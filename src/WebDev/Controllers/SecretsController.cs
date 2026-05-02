using Brigade.Admin.Data.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebDev.Controllers;

[ApiController, Route("api/secrets")]
[Authorize(Roles = "admin")]
public class SecretsController(ISecretsStore store) : ControllerBase
{
    [HttpPost("{id:guid}/reveal")]
    public async Task<IActionResult> Reveal(Guid id, CancellationToken ct = default)
    {
        var value = await store.DecryptAsync(id, ct);
        if (value is null) return NotFound();
        return Ok(new { value });
    }
}
