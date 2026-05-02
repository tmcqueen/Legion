using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Prompts;
using Legion.Admin.Data.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebDev.Controllers;

[ApiController, Route("api/agents/{agentId:guid}/prompts")]
[Authorize(Roles = "admin")]
public class AgentPromptsController(IPromptStore store) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAssignments(Guid agentId, CancellationToken ct = default)
    {
        var assignments = await store.GetAgentAssignmentsAsync((AgentOptionsId)agentId, ct);
        return Ok(assignments);
    }

    [HttpPost]
    public async Task<IActionResult> SetAssignments(
        Guid agentId, [FromBody] List<AssignmentItem> items, CancellationToken ct = default)
    {
        try
        {
            await store.SetAgentAssignmentsAsync(
                (AgentOptionsId)agentId,
                items.Select(i => ((PromptDefinitionId)i.DefinitionId, i.Order)),
                ct);
            return NoContent();
        }
        catch (ArgumentException ex) { return BadRequest(ex.Message); }
    }
}

public record AssignmentItem(Guid DefinitionId, int Order);
