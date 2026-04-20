using System.Security.Claims;
using Brigade.WebHost.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Brigade.WebHost.Tests.Controllers;

public class HelloWorldControllerTests
{
    [Fact]
    public void Hello_WithAuthenticatedUser_ReturnsOkWithName()
    {
        var controller = new HelloWorldController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, "testuser")], "TestScheme"))
            }
        };

        var result = controller.Hello() as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("testuser", json);
    }

    [Fact]
    public void Hello_WithUnauthenticatedUser_UserNameIsNull()
    {
        var controller = new HelloWorldController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity()) // no auth type = unauthenticated
            }
        };

        var result = controller.Hello() as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("Hello, !", json); // Name is null — real auth is enforced by [Authorize]
    }
}