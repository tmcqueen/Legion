using System.Security.Claims;
using Legion.WebHost.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Legion.WebHost.Tests.Controllers;

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
    public void Hello_WithNoNameClaim_ReturnsGreetingWithEmptyName()
    {
        var controller = new HelloWorldController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        var result = controller.Hello() as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("Hello, !", json);
    }
}