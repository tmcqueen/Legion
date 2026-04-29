using Brigade.Admin.Data.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using NSubstitute;
using WebDev.Controllers;
using Xunit;

namespace Brigade.Secrets.Tests;

public class SecretsControllerTests
{
    private readonly ISecretsStore _store = Substitute.For<ISecretsStore>();

    private SecretsController BuildController(bool isAdmin = true)
    {
        var controller = new SecretsController(_store);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new System.Security.Claims.ClaimsPrincipal(
                    new System.Security.Claims.ClaimsIdentity(
                        isAdmin
                            ? [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "admin")]
                            : [],
                        "TestScheme"))
            }
        };
        return controller;
    }

    [Fact]
    public async Task Reveal_ExistingSecret_ReturnsPlaintext()
    {
        _store.DecryptAsync(42).Returns("sk-secret-value");
        var controller = BuildController();

        var result = await controller.Reveal(42) as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("sk-secret-value", json);
    }

    [Fact]
    public async Task Reveal_NotFound_Returns404()
    {
        _store.DecryptAsync(99).Returns((string?)null);
        var controller = BuildController();

        var result = await controller.Reveal(99);

        Assert.IsType<NotFoundResult>(result);
    }
}
