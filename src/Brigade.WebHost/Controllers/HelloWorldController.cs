using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Validation.AspNetCore;

namespace Brigade.WebHost.Controllers;

[ApiController, Route("api")]
public class HelloWorldController : ControllerBase
{
    [HttpGet("hello"), Authorize(Policy = "BFFPolicy")]
    public IActionResult Hello() =>
        Ok(new { message = $"Hello, {User.Identity!.Name}!" });
}