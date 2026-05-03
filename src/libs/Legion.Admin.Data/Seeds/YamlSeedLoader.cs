using System.Reflection;
using System.Text.RegularExpressions;
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Legion.Admin.Data.Models.Providers;
using Legion.Admin.Data.Seeds.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Legion.Admin.Data.Seeds;

public class YamlSeedLoader(IConfiguration configuration, ILogger<YamlSeedLoader> logger)
{
    private static readonly string[] SensitiveFields = ["password", "clientSecret", "encryptedValue"];

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
                var interpolated = Interpolate(yaml);
                var doc = DeserializeDocument(interpolated);
                if (doc?.Entities is null) continue;

                foreach (var entity in doc.Entities)
                {
                    GuardSensitiveFields(file, entity);
                    Dispatch(payload, entity, file);
                }
            }
            catch (YamlException ex)
            {
                logger.LogError(ex, "Malformed YAML in '{File}' at line {Line} — skipping file",
                    Path.GetFileName(file), ex.Start.Line);
            }
        }

        return payload;
    }

    private static SeedDocument? DeserializeDocument(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithTypeDiscriminatingNodeDeserializer(o =>
            {
                o.AddKeyValueTypeDiscriminator<ISeedEntity>("seedType", SeedEntityRegistry.Map);
            })
            .Build();
        return deserializer.Deserialize<SeedDocument>(yaml);
    }

    private string Interpolate(string yaml) =>
        Regex.Replace(yaml, @"""?\$\{([^}]+)\}""?", match =>
        {
            var resolved = configuration[match.Groups[1].Value];
            // Preserve original placeholder if unresolved — caught later by GuardSensitiveFields.
            if (resolved is null) return match.Value;
            // Emit a YAML double-quoted scalar so values containing ':', '#', '\n', quotes,
            // etc. don't break document structure. Backslash and double-quote must be escaped.
            var escaped = resolved.Replace("\\", "\\\\").Replace("\"", "\\\"");
            return $"\"{escaped}\"";
        });

    private void GuardSensitiveFields(string fileName, ISeedEntity dto)
    {
        foreach (var field in SensitiveFields)
        {
            var prop = dto.GetType().GetProperty(field,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            var value = prop?.GetValue(dto) as string;
            if (value is null) continue;

            if (value.StartsWith("${"))
                throw new InvalidOperationException(
                    $"Seed file '{Path.GetFileName(fileName)}': '{field}' contains an unresolved placeholder '{value}'. " +
                    $"Set the config key via User Secrets or environment variables.");
        }
    }

    private void Dispatch(SeedPayload payload, ISeedEntity entity, string file)
    {
        switch (entity)
        {
            case SecretOptions s:
                if (payload.Secrets.Any(x => x.Path == s.Path))
                    LogDuplicate("secret", s.Path, file);
                else
                    payload.Secrets.Add(s);
                break;
            case ProviderOptions p:
                if (payload.Providers.Any(x => x.Name == p.Name))
                    LogDuplicate("provider", p.Name ?? "(null)", file);
                else
                    payload.Providers.Add(p);
                break;
            case AgentOptions a:
                if (payload.Agents.Any(x => x.Name == a.Name))
                    LogDuplicate("agent", a.Name ?? "(null)", file);
                else
                    payload.Agents.Add(a);
                break;
            case SeedUserDto u:
                if (payload.Users.Any(x => x.UserName == u.UserName))
                    LogDuplicate("user", u.UserName, file);
                else
                    payload.Users.Add(u);
                break;
            case OidcApplicationDto app:
                ValidatePermissions(app.Permissions, file);
                if (payload.OidcApplications.Any(x => x.ClientId == app.ClientId))
                    LogDuplicate("oidc-application", app.ClientId, file);
                else
                    payload.OidcApplications.Add(app);
                break;
            case OidcScopeDto sc:
                if (payload.OidcScopes.Any(x => x.Name == sc.Name))
                    LogDuplicate("oidc-scope", sc.Name, file);
                else
                    payload.OidcScopes.Add(sc);
                break;
            default:
                logger.LogWarning("Unhandled seed entity type {Type} in '{File}'",
                    entity.GetType().Name, Path.GetFileName(file));
                break;
        }
    }

    private void LogDuplicate(string kind, string key, string file) =>
        logger.LogWarning("Duplicate {Kind} '{Key}' in '{File}' — skipping",
            kind, key, Path.GetFileName(file));

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

    private sealed class SeedDocument
    {
        public List<ISeedEntity> Entities { get; set; } = [];
    }
}
