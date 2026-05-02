using Legion.Admin.Data.Models.Prompts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebDev.Services;

namespace WebDev.Controllers;

[ApiController, Route("api/prompts/import")]
[Authorize(Roles = "admin")]
public class ImportController(ImportService importService) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Import([FromBody] ImportRequest req, CancellationToken ct = default)
    {
        var (preview, error) = await importService.ParsePreviewAsync(req.Url, req.MarkdownContent, req.Filename, ct);
        return error is null ? Ok(preview) : BadRequest(error);
    }

    [HttpPost("confirm")]
    public async Task<IActionResult> ConfirmImport([FromBody] ConfirmImportRequest req, CancellationToken ct = default)
    {
        var createdBy = $"{User.FindFirst("sub")?.Value}:{User.Identity?.Name}";
        var results = await importService.ConfirmAsync(req.Items, req.PublishImmediately, createdBy, ct);
        return results.Any(r => !r.Success) ? StatusCode(207, results) : Ok(results);
    }
}

public record ImportRequest(string? Url, string? MarkdownContent, string? Filename);
public record ImportPreview(string Filename, string SuggestedPath, PromptType Type, PromptCategory Category, string Content, bool FrontmatterDetected);
public record ConfirmImportItem(string Path, PromptType Type, PromptCategory Category, string Content);
public record ConfirmImportRequest(List<ConfirmImportItem> Items, bool PublishImmediately);
