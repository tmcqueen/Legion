using Legion.Admin.Data.Models;
using Legion.Admin.Data.Services;
using Legion.Admin.Data.Stores;
using NSubstitute;
using Xunit;

namespace Legion.Secrets.Tests;

public class SecretsManagerTests
{
    private readonly ISecretsStore _store = Substitute.For<ISecretsStore>();
    private readonly SecretsManager _sut;

    public SecretsManagerTests() => _sut = new SecretsManager(_store);

    [Theory]
    [InlineData("secret://openai/key")]
    [InlineData("SECRET://openai/key")]
    public void IsSecretReference_WithSecretUri_ReturnsTrue(string value) =>
        Assert.True(_sut.IsSecretReference(value));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("sk-abc123")]
    [InlineData("https://example.com")]
    public void IsSecretReference_WithNonSecretValue_ReturnsFalse(string? value) =>
        Assert.False(_sut.IsSecretReference(value));

    [Fact]
    public async Task ResolveAsync_LeafPath_TextPlain_ReturnsPlainValue()
    {
        var leaf = new SecretOptions { Id = SecretOptionsId.New(), Path = "openai/key" };
        _store.FindByPathAsync("openai/key").Returns(leaf);
        _store.DecryptAsync(leaf.Id).Returns("sk-secret-value");

        var result = await _sut.ResolveAsync(new SecretRequest { Path = "secret://openai/key" });

        Assert.Equal("sk-secret-value", result);
    }

    [Fact]
    public async Task ResolveAsync_LeafPath_TextJson_ReturnsJsonObject()
    {
        var leaf = new SecretOptions { Id = SecretOptionsId.New(), Path = "openai/client_ids/test" };
        _store.FindByPathAsync("openai/client_ids/test").Returns(leaf);
        _store.DecryptAsync(leaf.Id).Returns("foo");

        var result = await _sut.ResolveAsync(new SecretRequest
        {
            Path = "secret://openai/client_ids/test",
            MediaType = "text/json"
        });

        Assert.Equal("{\"key\":\"test\",\"value\":\"foo\"}", result);
    }

    [Fact]
    public async Task ResolveAsync_CollectionPath_TextPlain_ReturnsUrlEncodedPairs()
    {
        _store.FindByPathAsync("openai/client_ids").Returns((SecretOptions?)null);
        var test = new SecretOptions { Id = SecretOptionsId.New(), Path = "openai/client_ids/test" };
        var prod = new SecretOptions { Id = SecretOptionsId.New(), Path = "openai/client_ids/prod" };
        _store.GetChildrenAsync("openai/client_ids").Returns([test, prod]);
        _store.DecryptAsync(test.Id).Returns("foo");
        _store.DecryptAsync(prod.Id).Returns("bar");

        var result = await _sut.ResolveAsync(new SecretRequest { Path = "secret://openai/client_ids" });

        Assert.Equal("test=foo&prod=bar", result);
    }

    [Fact]
    public async Task ResolveAsync_CollectionPath_TextJson_ReturnsJsonArray()
    {
        _store.FindByPathAsync("openai/client_ids").Returns((SecretOptions?)null);
        var child = new SecretOptions { Id = SecretOptionsId.New(), Path = "openai/client_ids/test" };
        _store.GetChildrenAsync("openai/client_ids").Returns([child]);
        _store.DecryptAsync(child.Id).Returns("foo");

        var result = await _sut.ResolveAsync(new SecretRequest
        {
            Path = "secret://openai/client_ids",
            MediaType = "text/json"
        });

        Assert.Equal("[{\"key\":\"test\",\"value\":\"foo\"}]", result);
    }

    [Fact]
    public async Task ResolveAsync_UnknownPath_ReturnsNull()
    {
        _store.FindByPathAsync("nonexistent").Returns((SecretOptions?)null);
        _store.GetChildrenAsync("nonexistent").Returns([]);

        var result = await _sut.ResolveAsync(new SecretRequest { Path = "secret://nonexistent" });

        Assert.Null(result);
    }
}
