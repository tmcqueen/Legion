using Legion.Admin.Data.Services;
using Legion.Agents.Http;
using NSubstitute;
using System.Net;
using Xunit;

namespace Legion.Secrets.Tests;

public class SecretResolvingHandlerTests
{
    private readonly ISecretsManager _secrets = Substitute.For<ISecretsManager>();

    private HttpClient BuildClient(HttpMessageHandler inner)
    {
        var handler = new SecretResolvingHandler(_secrets) { InnerHandler = inner };
        return new HttpClient(handler) { BaseAddress = new Uri("https://example.com") };
    }

    [Fact]
    public async Task SendAsync_NonSecretHeader_PassesThrough()
    {
        var fake = new FakeHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var client = BuildClient(fake);

        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.Add("Authorization", "Bearer raw-token");

        await client.SendAsync(request);

        _secrets.DidNotReceive().IsSecretReference(Arg.Any<string>());
    }

    [Fact]
    public async Task SendAsync_SecretHeader_ResolvesBeforeSending()
    {
        _secrets.IsSecretReference("secret://openai/key").Returns(true);
        _secrets.ResolveAsync(Arg.Any<SecretRequest>(), Arg.Any<CancellationToken>()).Returns("sk-resolved");

        string? capturedAuth = null;
        var fake = new FakeHandler(req =>
        {
            capturedAuth = req.Headers.Authorization?.Parameter;
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var client = BuildClient(fake);

        var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.TryAddWithoutValidation("Authorization", "Bearer secret://openai/key");

        await client.SendAsync(request);

        Assert.Contains("sk-resolved", capturedAuth ?? "");
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(handler(request));
    }
}
