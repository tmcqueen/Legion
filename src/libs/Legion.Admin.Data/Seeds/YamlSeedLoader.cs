using System.Reflection;
using System.Text.RegularExpressions;
using Legion.Admin.Data.Seeds.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Legion.Admin.Data.Seeds;

public class YamlSeedLoader(IConfiguration configuration, ILogger<YamlSeedLoader> logger)
{
    private static readonly string[] SensitiveFields = ["password", "clientSecret"];

    private static readonly string[] KnownPermissionPrefixes =
        ["ept:", "gt:", "rt:", "scp:"];

    public SeedPayload LoadAll(string seedFolderPath)
    {
        var payload = new SeedPayload();

        if (!Directory.Exists(seedFolderPath))
        {
            logger.LogWarning("Seed folder not found at '{Path}' — skipping seed load", seedFolderPath);
            return payload;
        }

        var files = Directory
            .EnumerateFiles(seedFolderPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => f.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            try
            {
                var yaml = File.ReadAllText(file);
                var document = Deserialize(yaml);
                InterpolateGraph(document);
                Merge(payload, document, file);
            }
            catch (YamlException ex)
            {
                logger.LogError(ex, "Malformed YAML in '{File}' at line {Line} — skipping file",
                    Path.GetFileName(file), ex.Start.Line);
            }
        }

        return payload;
    }

    private static Dictionary<string, object> Deserialize(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<Dictionary<string, object>>(yaml) ?? [];
    }

    private void InterpolateGraph(object? node)
    {
        switch (node)
        {
            // YamlDotNet returns Dictionary<string, object> for the root document
            // and Dictionary<object, object> for nested mappings — handle both
            case Dictionary<string, object> rootDict:
                foreach (var key in rootDict.Keys.ToList())
                {
                    if (rootDict[key] is string s)
                        rootDict[key] = Interpolate(s);
                    else
                        InterpolateGraph(rootDict[key]);
                }
                break;
            case Dictionary<object, object> nestedDict:
                foreach (var key in nestedDict.Keys.ToList())
                {
                    if (nestedDict[key] is string s)
                        nestedDict[key] = Interpolate(s);
                    else
                        InterpolateGraph(nestedDict[key]);
                }
                break;
            case List<object> list:
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is string s)
                        list[i] = Interpolate(s);
                    else
                        InterpolateGraph(list[i]);
                }
                break;
        }
    }

    private string Interpolate(string value) =>
        Regex.Replace(value, @"\$\{([^}]+)\}", match =>
            configuration[match.Groups[1].Value] ?? match.Value);

    private void GuardSensitiveFields(string fileName, object dto)
    {
        foreach (var field in SensitiveFields)
        {
            var prop = dto.GetType().GetProperty(field,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            var value = prop?.GetValue(dto) as string;
            if (value is null) continue;

            if (value.StartsWith("${"))
                throw new InvalidOperationException(
                    $"Seed file '{fileName}': '{field}' contains an unresolved placeholder '{value}'. " +
                    $"Set the config key via User Secrets or environment variables.");
        }
    }

    private void Merge(SeedPayload payload, Dictionary<string, object> document, string file)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        foreach (var (key, value) in document)
        {
            switch (key)
            {
                case "agents":
                    MergeList<SeedAgentDto>(payload.Agents, value, deserializer, file, key,
                        dto => dto.Name, "name");
                    break;
                case "users":
                    var users = DeserializeList<SeedUserDto>(value, deserializer);
                    foreach (var dto in users)
                    {
                        GuardSensitiveFields(file, dto);
                        if (payload.Users.Any(u => u.UserName == dto.UserName))
                        {
                            logger.LogWarning("Duplicate user '{UserName}' in '{File}' — skipping",
                                dto.UserName, Path.GetFileName(file));
                            continue;
                        }
                        payload.Users.Add(dto);
                    }
                    break;
                case "oidc-applications":
                    var apps = DeserializeList<OidcApplicationDto>(value, deserializer);
                    foreach (var dto in apps)
                    {
                        GuardSensitiveFields(file, dto);
                        ValidatePermissions(dto.Permissions, file);
                        if (payload.OidcApplications.Any(a => a.ClientId == dto.ClientId))
                        {
                            logger.LogWarning("Duplicate clientId '{ClientId}' in '{File}' — skipping",
                                dto.ClientId, Path.GetFileName(file));
                            continue;
                        }
                        payload.OidcApplications.Add(dto);
                    }
                    break;
                case "oidc-scopes":
                    MergeList<OidcScopeDto>(payload.OidcScopes, value, deserializer, file, key,
                        dto => dto.Name, "name");
                    break;
                default:
                    logger.LogWarning("Unknown seed key '{Key}' in '{File}' — skipping",
                        key, Path.GetFileName(file));
                    break;
            }
        }
    }

    private void MergeList<T>(List<T> target, object rawValue, IDeserializer deserializer,
        string file, string key, Func<T, string> getKey, string keyName)
    {
        var items = DeserializeList<T>(rawValue, deserializer);
        foreach (var item in items)
        {
            var itemKey = getKey(item);
            if (target.Any(existing => getKey(existing) == itemKey))
            {
                logger.LogWarning("Duplicate {KeyName} '{Key}' in '{File}' — skipping",
                    keyName, itemKey, Path.GetFileName(file));
                continue;
            }
            target.Add(item);
        }
    }

    private static List<T> DeserializeList<T>(object rawValue, IDeserializer deserializer)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var yaml = serializer.Serialize(rawValue);
        return deserializer.Deserialize<List<T>>(yaml) ?? [];
    }

    private void ValidatePermissions(List<string> permissions, string file)
    {
        foreach (var permission in permissions)
        {
            if (!KnownPermissionPrefixes.Any(p => permission.StartsWith(p, StringComparison.Ordinal)))
                logger.LogWarning(
                    "Unrecognised permission prefix in '{File}': '{Permission}'",
                    Path.GetFileName(file), permission);
        }
    }
}
