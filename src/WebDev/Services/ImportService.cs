using System.Net;
using System.Text;
using Legion.Admin.Data.Models.Prompts;
using Legion.Admin.Data.Stores;
using WebDev.Controllers;

namespace WebDev.Services;

public class ImportService(IPromptStore store, IHttpClientFactory httpClientFactory)
{
    private const long MaxResponseBytes = 10 * 1024 * 1024;
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(15);

    public async Task<(ImportPreview? preview, string? error)> ParsePreviewAsync(
        string? url, string? markdownContent, string? filename, CancellationToken ct = default)
    {
        string content;
        string suggestedFilename;

        if (url is not null)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return (null, "Invalid URL.");
            if (uri.Scheme != "https")
                return (null, "Only https:// URLs are allowed.");

            var ssrfError = await CheckSsrfAsync(uri);
            if (ssrfError is not null)
                return (null, ssrfError);

            var client = httpClientFactory.CreateClient("import");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(FetchTimeout);
            try
            {
                var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is long len && len > MaxResponseBytes)
                    return (null, "Response exceeds 10 MB limit.");
                await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                using var reader = new StreamReader(stream);
                var sb = new StringBuilder();
                var buf = new char[8192];
                int n;
                while ((n = await reader.ReadAsync(buf, cts.Token)) > 0)
                {
                    sb.Append(buf, 0, n);
                    if (sb.Length > MaxResponseBytes) return (null, "Response exceeds 10 MB limit.");
                }
                content = sb.ToString();
            }
            catch (OperationCanceledException) { return (null, "Fetch timed out."); }
            catch (HttpRequestException ex) { return (null, $"Fetch failed: {ex.Message}"); }

            suggestedFilename = Path.GetFileName(uri.LocalPath);
        }
        else if (markdownContent is not null)
        {
            content = markdownContent;
            suggestedFilename = filename ?? "paste.md";
        }
        else
        {
            return (null, "Provide either 'url' or 'markdownContent'.");
        }

        return (ParseImportPreview(content, suggestedFilename), null);
    }

    public async Task<List<ImportResult>> ConfirmAsync(
        IEnumerable<ConfirmImportItem> items, bool publishImmediately, string createdBy, CancellationToken ct = default)
    {
        var results = new List<ImportResult>();
        foreach (var item in items)
        {
            try
            {
                var existing = (await store.SearchDefinitionsAsync(item.Path, ct: ct))
                    .FirstOrDefault(d => d.Path == item.Path);
                var def = existing ?? await store.CreateDefinitionAsync(
                    item.Path, item.Type, item.Category, false, createdBy, ct);

                var (frontmatter, body) = SplitFrontmatter(item.Content);
                var version = await store.CreateDraftAsync(def.Id, body, frontmatter, createdBy, "Imported", ct);

                if (publishImmediately)
                    await store.PublishDraftAsync(version.Id, ct);

                results.Add(new ImportResult(item.Path, true, null, (Guid)version.Id));
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                results.Add(new ImportResult(item.Path, false, ex.Message, null));
            }
        }
        return results;
    }

    internal static ImportPreview ParseImportPreview(string content, string filename)
    {
        var (frontmatter, body) = SplitFrontmatter(content);
        var (suggestedPath, suggestedType) = InferPathAndType(filename);
        var category = InferCategory(frontmatter);
        return new ImportPreview(filename, suggestedPath, suggestedType, category, content, frontmatter is not null);
    }

    internal static (string? frontmatter, string body) SplitFrontmatter(string content)
    {
        var lines = content.Split('\n');
        if (lines.Length < 2 || lines[0].Trim() != "---") return (null, content);
        var end = Array.FindIndex(lines, 1, l => l.Trim() == "---");
        if (end < 0) return (null, content);
        var frontmatter = string.Join('\n', lines[1..end]).Trim();
        var body = string.Join('\n', lines[(end + 1)..]).TrimStart('\n');
        return (frontmatter, body);
    }

    private static (string path, PromptType type) InferPathAndType(string filename)
    {
        var name = Path.GetFileNameWithoutExtension(filename);
        if (name.StartsWith("agent-prompt-", StringComparison.OrdinalIgnoreCase))
            return ($"/Agents/Prompts/{Capitalize(name[13..])}", PromptType.Prompt);
        if (name.StartsWith("skill-", StringComparison.OrdinalIgnoreCase))
            return ($"/Skills/{Capitalize(name[6..])}", PromptType.Skill);
        if (name.StartsWith("tool-description-", StringComparison.OrdinalIgnoreCase))
            return ($"/Tools/{Capitalize(name[17..])}", PromptType.ToolDescription);
        return ($"/Imported/{Capitalize(name)}", PromptType.Prompt);
    }

    private static PromptCategory InferCategory(string? frontmatter) =>
        frontmatter?.Contains("Foundation", StringComparison.OrdinalIgnoreCase) == true ? PromptCategory.Foundation :
        frontmatter?.Contains("Constraints", StringComparison.OrdinalIgnoreCase) == true ? PromptCategory.Constraints :
        frontmatter?.Contains("Overrides", StringComparison.OrdinalIgnoreCase) == true ? PromptCategory.Overrides :
        PromptCategory.TaskSpecific;

    private static string Capitalize(string s) =>
        string.IsNullOrEmpty(s) ? s :
        string.Join("-", s.Split('-').Select(p => string.IsNullOrEmpty(p) ? p : char.ToUpperInvariant(p[0]) + p[1..]));

    private static async Task<string?> CheckSsrfAsync(Uri uri)
    {
        IPAddress[] addresses;
        try { addresses = await Dns.GetHostAddressesAsync(uri.Host); }
        catch { return $"Cannot resolve host '{uri.Host}'."; }

        foreach (var ip in addresses)
        {
            if (IsBlockedIp(ip))
                return $"Requests to {ip} are not allowed.";
        }
        return null;
    }

    private static bool IsBlockedIp(IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (IPAddress.IsLoopback(ip)) return true;

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
            return ip.Equals(IPAddress.IPv6Loopback)
                || ip.IsIPv6LinkLocal
                || IsIPv6Ula(ip);

        var bytes = ip.GetAddressBytes();
        return bytes[0] == 10 ||
               (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
               (bytes[0] == 192 && bytes[1] == 168) ||
               (bytes[0] == 169 && bytes[1] == 254) ||
               (bytes[0] == 100 && bytes[1] >= 64 && bytes[1] <= 127);
    }

    private static bool IsIPv6Ula(IPAddress ip)
    {
        var bytes = ip.GetAddressBytes();
        return (bytes[0] & 0xFE) == 0xFC;
    }
}

public record ImportResult(string Path, bool Success, string? ErrorMessage, Guid? VersionId);
