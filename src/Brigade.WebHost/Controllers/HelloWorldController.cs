using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brigade.WebHost.Controllers;

[ApiController, Route("api")]
public class HelloWorldController : ControllerBase
{
    [HttpGet("hello"), Authorize(AuthenticationSchemes = "Bearer")]
    public IActionResult Hello() =>
        Ok(new { message = $"Hello, {User.Identity!.Name}!" });
}