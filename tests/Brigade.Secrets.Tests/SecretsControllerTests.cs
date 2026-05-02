using Brigade.Admin.Data.Stores;
using Brigade.Admin.Data.Models;
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
        var id = SecretOptionsId.New();
        _store.DecryptAsync(id.Value).Returns("sk-secret-value");
        var controller = BuildController();

        var result = await controller.Reveal(id.Value) as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("sk-secret-value", json);
    }

    [Fact]
    public async Task Reveal_NotFound_Returns404()
    {
        var id = SecretOptionsId.New();
        _store.DecryptAsync(id.Value).Returns((string?)null);
        var controller = BuildController();

        var result = await controller.Reveal(id.Value);

        Assert.IsType<NotFoundResult>(result);
    }
}
