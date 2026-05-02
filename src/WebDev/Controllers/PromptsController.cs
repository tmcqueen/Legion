using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Prompts;
using Legion.Admin.Data.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebDev.Controllers;

[ApiController, Route("api/prompts")]
[Authorize(Roles = "admin")]
public class PromptsController(IPromptStore store) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? query = null,
        [FromQuery] string? type = null,
        [FromQuery] bool includeDeleted = false,
        CancellationToken ct = default)
    {
        PromptType? typeFilter = type is not null && Enum.TryParse<PromptType>(type, out var t) ? t : null;
        var defs = await store.SearchDefinitionsAsync(query ?? string.Empty, typeFilter, includeDeleted, ct);
        return Ok(defs);
    }

    [HttpGet("by-path")]
    public async Task<IActionResult> GetByPath([FromQuery] string path, CancellationToken ct = default)
    {
        var version = await store.GetPublishedPromptAsync(path, ct);
        return version is null ? NotFound() : Ok(version);
    }

    [HttpGet("by-path/history")]
    public async Task<IActionResult> GetHistory([FromQuery] string path, CancellationToken ct = default)
    {
        var defs = await store.SearchDefinitionsAsync(path, includeDeleted: true, ct: ct);
        var def = defs.FirstOrDefault(d => d.Path == path);
        if (def is null) return NotFound();
        var history = await store.GetPromptHistoryAsync(def.Id, ct);
        return Ok(history);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetVersion(Guid id, CancellationToken ct = default)
    {
        var version = await store.GetPromptVersionAsync((PromptVersionId)id, ct);
        return version is null ? NotFound() : Ok(version);
    }

    [HttpPost("definitions")]
    public async Task<IActionResult> CreateDefinition(
        [FromBody] CreateDefinitionRequest req, CancellationToken ct = default)
    {
        try
        {
            var createdBy = $"{User.FindFirst("sub")?.Value}:{User.Identity?.Name}";
            var def = await store.CreateDefinitionAsync(req.Path, req.Type, req.Category, req.IsDefaultIncluded, createdBy, ct);
            return CreatedAtAction(nameof(GetByPath), new { path = def.Path }, def);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpPost("drafts")]
    public async Task<IActionResult> CreateDraft(
        [FromBody] CreateDraftRequest req, CancellationToken ct = default)
    {
        try
        {
            var createdBy = $"{User.FindFirst("sub")?.Value}:{User.Identity?.Name}";
            var version = await store.CreateDraftAsync(req.DefinitionId, req.Content, req.Frontmatter, createdBy, req.Notes, ct);
            return CreatedAtAction(nameof(GetVersion), new { id = (Guid)version.Id }, version);
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpPut("drafts/{id:guid}")]
    public async Task<IActionResult> UpdateDraft(
        Guid id, [FromBody] UpdateDraftRequest req, CancellationToken ct = default)
    {
        try
        {
            await store.UpdateDraftAsync((PromptVersionId)id, req.Content, req.Frontmatter, ct);
            return NoContent();
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpPost("drafts/{id:guid}/publish")]
    public async Task<IActionResult> PublishDraft(Guid id, CancellationToken ct = default)
    {
        try
        {
            await store.PublishDraftAsync((PromptVersionId)id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpDelete("drafts/{id:guid}")]
    public async Task<IActionResult> DiscardDraft(Guid id, CancellationToken ct = default)
    {
        try
        {
            await store.DiscardDraftAsync((PromptVersionId)id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpPost("{id:guid}/republish")]
    public async Task<IActionResult> RepublishArchived(Guid id, CancellationToken ct = default)
    {
        try
        {
            await store.RepublishArchivedAsync((PromptVersionId)id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
        catch (InvalidOperationException ex) { return Conflict(ex.Message); }
    }

    [HttpDelete("definitions")]
    public async Task<IActionResult> DeleteDefinition([FromQuery] string path, CancellationToken ct = default)
    {
        var defs = await store.SearchDefinitionsAsync(path, ct: ct);
        var def = defs.FirstOrDefault(d => d.Path == path);
        if (def is null) return NotFound();
        try
        {
            await store.DeleteDefinitionAsync(def.Id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
    }
}

public record CreateDefinitionRequest(string Path, PromptType Type, PromptCategory Category, bool IsDefaultIncluded);
public record CreateDraftRequest(PromptDefinitionId DefinitionId, string Content, string? Frontmatter, string? Notes);
public record UpdateDraftRequest(string Content, string? Frontmatter);
