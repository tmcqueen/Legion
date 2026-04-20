using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brigade.WebHost.Controllers;

[ApiController, Route("api")]
public class HelloWorldController : ControllerBase
{
    [HttpGet("hello"), Authorize]
    public IActionResult Hello() =>
        Ok(new { message = $"Hello, {User.Identity!.Name}!" });
}