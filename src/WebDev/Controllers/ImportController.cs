using System.Net;
using System.Text;
using Legion.Admin.Data.Models.Prompts;
using Legion.Admin.Data.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebDev.Controllers;

[ApiController, Route("api/prompts/import")]
[Authorize(Roles = "admin")]
public class ImportController(IPromptStore store, IHttpClientFactory httpClientFactory) : ControllerBase
{
    private const long MaxResponseBytes = 10 * 1024 * 1024;
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(15);

    [HttpPost]
    public async Task<IActionResult> Import([FromBody] ImportRequest req, CancellationToken ct = default)
    {
        string content;
        string suggestedFilename;

        if (req.Url is not null)
        {
            if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var uri))
                return BadRequest("Invalid URL.");
            if (uri.Scheme != "https")
                return BadRequest("Only https:// URLs are allowed.");

            var ssrfError = await CheckSsrfAsync(uri);
            if (ssrfError is not null)
                return BadRequest(ssrfError);

            var client = httpClientFactory.CreateClient("import");
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(FetchTimeout);
            try
            {
                var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is long len && len > MaxResponseBytes)
                    return BadRequest("Response exceeds 10 MB limit.");
                await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                using var reader = new System.IO.StreamReader(stream);
                var sb = new StringBuilder();
                var buf = new char[8192];
                int n;
                while ((n = await reader.ReadAsync(buf, cts.Token)) > 0)
                {
                    sb.Append(buf, 0, n);
                    if (sb.Length > MaxResponseBytes) return BadRequest("Response exceeds 10 MB limit.");
                }
                content = sb.ToString();
            }
            catch (OperationCanceledException) { return StatusCode(504, "Fetch timed out."); }
            catch (HttpRequestException ex) { return BadRequest($"Fetch failed: {ex.Message}"); }

            suggestedFilename = Path.GetFileName(uri.LocalPath);
        }
        else if (req.MarkdownContent is not null)
        {
            content = req.MarkdownContent;
            suggestedFilename = req.Filename ?? "paste.md";
        }
        else
        {
            return BadRequest("Provide either 'url' or 'markdownContent'.");
        }

        var preview = ParseImportPreview(content, suggestedFilename);
        return Ok(preview);
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmImport(
        [FromBody] ConfirmImportRequest req, CancellationToken ct = default)
    {
        var createdBy = $"{User.FindFirst("sub")?.Value}:{User.Identity?.Name}";
        var results = new List<object>();

        foreach (var item in req.Items)
        {
            try
            {
                var existing = (await store.SearchDefinitionsAsync(item.Path, ct: ct))
                    .FirstOrDefault(d => d.Path == item.Path);
                var def = existing ?? await store.CreateDefinitionAsync(
                    item.Path, item.Type, item.Category, false, createdBy, ct);

                var (frontmatter, body) = SplitFrontmatter(item.Content);
                var version = await store.CreateDraftAsync(def.Id, body, frontmatter, createdBy, "Imported", ct);

                if (req.PublishImmediately)
                    await store.PublishDraftAsync(version.Id, ct);

                results.Add(new { path = item.Path, status = "ok", versionId = (Guid)version.Id });
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                results.Add(new { path = item.Path, status = "error", message = ex.Message });
            }
        }

        return Ok(results);
    }

    private static ImportPreview ParseImportPreview(string content, string filename)
    {
        var (frontmatter, body) = SplitFrontmatter(content);
        var (suggestedPath, suggestedType) = InferPathAndType(filename);
        var category = InferCategory(frontmatter);
        return new ImportPreview(filename, suggestedPath, suggestedType, category, content, frontmatter is not null);
    }

    private static (string? frontmatter, string body) SplitFrontmatter(string content)
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
        // Unwrap IPv4-mapped IPv6 addresses (::ffff:x.x.x.x) before family checks
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
        return (bytes[0] & 0xFE) == 0xFC; // fc00::/7 ULA range
    }
}

public record ImportRequest(string? Url, string? MarkdownContent, string? Filename);
public record ImportPreview(string Filename, string SuggestedPath, PromptType Type, PromptCategory Category, string Content, bool FrontmatterDetected);
public record ConfirmImportItem(string Path, PromptType Type, PromptCategory Category, string Content);
public record ConfirmImportRequest(List<ConfirmImportItem> Items, bool PublishImmediately);
