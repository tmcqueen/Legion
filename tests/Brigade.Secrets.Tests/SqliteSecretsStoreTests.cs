using Brigade.Admin.Data;
using Brigade.Admin.Data.Sqlite.Stores;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Brigade.Secrets.Tests;

public class SqliteSecretsStoreTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;
    private readonly SqliteSecretsStore _sut;

    public SqliteSecretsStoreTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _sut = new SqliteSecretsStore(_db);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task CreateAsync_StoresPlaintext()
    {
        var secret = await _sut.CreateAsync("openai/key", "My API key", "sk-secret");

        Assert.Equal("openai/key", secret.Path);
        Assert.Equal("My API key", secret.Description);
        Assert.Equal("sk-secret", secret.EncryptedValue);
    }

    [Fact]
    public async Task DecryptAsync_ReturnsStoredValue()
    {
        var secret = await _sut.CreateAsync("openai/key", null, "sk-secret");

        var value = await _sut.DecryptAsync(secret.Id);

        Assert.Equal("sk-secret", value);
    }

    [Fact]
    public async Task FindByPathAsync_ExactMatch_ReturnsSecret()
    {
        await _sut.CreateAsync("openai/key", null, "sk-secret");

        var found = await _sut.FindByPathAsync("openai/key");

        Assert.NotNull(found);
        Assert.Equal("openai/key", found.Path);
    }

    [Fact]
    public async Task FindByPathAsync_NoMatch_ReturnsNull()
    {
        var found = await _sut.FindByPathAsync("nonexistent");
        Assert.Null(found);
    }

    [Fact]
    public async Task GetChildrenAsync_ReturnsDirectChildrenOnly()
    {
        await _sut.CreateAsync("openai/client_ids/test", null, "foo");
        await _sut.CreateAsync("openai/client_ids/prod", null, "bar");
        await _sut.CreateAsync("openai/client_ids/group/nested", null, "baz");

        var children = await _sut.GetChildrenAsync("openai/client_ids");

        Assert.Equal(2, children.Count);
        Assert.All(children, c => Assert.DoesNotContain("group", c.Path));
    }

    [Fact]
    public async Task UpdateValueAsync_ChangesStoredValue()
    {
        var secret = await _sut.CreateAsync("openai/key", null, "old-value");

        await _sut.UpdateValueAsync(secret.Id, "new-value");

        var updated = await _sut.FindByPathAsync("openai/key");
        Assert.Equal("new-value", updated!.EncryptedValue);
    }

    [Fact]
    public async Task DeleteAsync_RemovesSecret()
    {
        var secret = await _sut.CreateAsync("openai/key", null, "sk-secret");

        await _sut.DeleteAsync(secret.Id);

        var found = await _sut.FindByPathAsync("openai/key");
        Assert.Null(found);
    }
}
