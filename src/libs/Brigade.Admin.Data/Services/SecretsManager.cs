using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Stores;
using System.Text.Json;
using System.Web;

namespace Brigade.Admin.Data.Services;

public record SecretRequest
{
    public string Path { get; init; } = string.Empty;
    public string MediaType { get; init; } = "text/plain";
}

public interface ISecretsManager
{
    bool IsSecretReference(string? value);
    Task<string?> ResolveAsync(SecretRequest request, CancellationToken ct = default);
}

public class SecretsManager(ISecretsStore store) : ISecretsManager
{
    private const string Scheme = "secret://";

    public bool IsSecretReference(string? value) =>
        value?.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) == true;

    public async Task<string?> ResolveAsync(SecretRequest request, CancellationToken ct = default)
    {
        var path = request.Path.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)
            ? request.Path[Scheme.Length..]
            : request.Path;

        var leaf = await store.FindByPathAsync(path, ct);
        if (leaf is not null)
        {
            var value = await store.DecryptAsync(leaf.Id, ct);
            if (request.MediaType == "text/json")
            {
                var key = path.Split('/').Last();
                return JsonSerializer.Serialize(new { key, value });
            }
            return value;
        }

        var children = await store.GetChildrenAsync(path, ct);
        if (children.Count == 0) return null;

        var pairs = new List<(string key, string? val)>();
        foreach (var child in children)
        {
            var childKey = child.Path.Split('/').Last();
            var childValue = await store.DecryptAsync(child.Id, ct);
            pairs.Add((childKey, childValue));
        }

        if (request.MediaType == "text/json")
        {
            var items = pairs.Select(p => new { key = p.key, value = p.val });
            return JsonSerializer.Serialize(items);
        }

        return string.Join("&", pairs.Select(p => $"{HttpUtility.UrlEncode(p.key)}={HttpUtility.UrlEncode(p.val)}"));
    }
}
