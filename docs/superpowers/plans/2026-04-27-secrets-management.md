# Secrets Management Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace plaintext `ApiToken`, `McpServerHeaders.Value`, and `McpServerOptions.CommandLine` storage with a path-addressable secret registry that encrypts at rest in PostgreSQL and stores plaintext in SQLite for development.

**Architecture:** A new `SecretOptions` EF entity stores secrets by path (e.g., `openai/client_ids/test`). Resources reference secrets via `secret://` URIs stored in their existing string fields — no existing model changes. An `ISecretsManager` scoped service resolves URIs at runtime; a `DelegatingHandler` auto-resolves them in outbound AI provider HTTP calls. Blazor UI manages secrets via a tree view and forms get a raw/secret mode toggle.

**Tech Stack:** .NET 10, EF Core, Npgsql (pgcrypto extension), SQLite, Radzen Blazor, xUnit

---

## File Map

### New files
| File | Responsibility |
|---|---|
| `src/libs/Brigade.Admin.Data.Models/SecretOptions.cs` | Entity record |
| `src/libs/Brigade.Admin.Data/Configurations/SecretOptionsConfiguration.cs` | EF fluent config |
| `src/libs/Brigade.Admin.Data/Stores/SecretsStore.cs` | `ISecretsStore` interface |
| `src/libs/Brigade.Admin.Data/Services/SecretsManager.cs` | `ISecretsManager` interface + implementation |
| `src/libs/Brigade.Admin.Data.Sqlite/Stores/SqliteSecretsStore.cs` | EF plaintext store |
| `src/libs/Brigade.Admin.Data.PostgreSQL/Stores/PostgreSqlSecretsStore.cs` | pgcrypto raw-SQL store |
| `src/Brigade.Agents/Http/SecretResolvingHandler.cs` | Outbound HTTP handler |
| `src/WebDev/Controllers/SecretsController.cs` | `POST /api/secrets/{id}/reveal` |
| `src/WebDev/Components/Pages/Secrets.razor` | Secrets tree UI page |
| `tests/Brigade.Secrets.Tests/Brigade.Secrets.Tests.csproj` | Test project |
| `tests/Brigade.Secrets.Tests/SecretsManagerTests.cs` | SecretsManager unit tests |
| `tests/Brigade.Secrets.Tests/SqliteSecretsStoreTests.cs` | SqliteSecretsStore integration tests |
| `tests/Brigade.Secrets.Tests/SecretResolvingHandlerTests.cs` | Handler unit tests |
| `tests/Brigade.Secrets.Tests/SecretsControllerTests.cs` | Controller unit tests |
| EF migrations (PostgreSQL + SQLite) | `Secrets` table |

### Modified files
| File | Change |
|---|---|
| `src/libs/Brigade.Admin.Data/AppDbContext.cs` | Add `DbSet<SecretOptions> Secrets` |
| `src/libs/Brigade.Admin.Data/Extensions/AgentDbContextExtensions.cs` | Register `ISecretsManager` |
| `src/libs/Brigade.Admin.Data.PostgreSQL/Extensions/PostgreSqlExtensions.cs` | Register `PostgreSqlSecretsStore` |
| `src/libs/Brigade.Admin.Data.Sqlite/Extensions/SqliteExtensions.cs` | Register `SqliteSecretsStore` |
| `src/Brigade.Agents/Brigade.Agents.csproj` | Add `<ProjectReference>` to `Brigade.Admin.Data` |
| `src/Brigade.Agents/Providers/AgentFactory.cs` | Accept `ISecretsManager`, resolve `ApiToken` before building agents |
| `src/libs/Brigade.Admin.UI/Components/Sidebar.razor` | Add "Secrets" nav item |
| `src/libs/Brigade.Admin.UI/Pages/Providers/Providers.Update.razor` | Secret mode toggle for `ApiToken` |
| `src/libs/Brigade.Admin.UI/Pages/Mcps/Mcps.Update.razor` | Secret mode toggle for `CommandLine` + header `Value` |

---

## Task 1: SecretOptions Model

**Files:**
- Create: `src/libs/Brigade.Admin.Data.Models/SecretOptions.cs`

- [ ] **Step 1: Create the model**

```csharp
// src/libs/Brigade.Admin.Data.Models/SecretOptions.cs
namespace Brigade.Admin.Data.Models;

public record SecretOptions
{
    public int Id { get; init; }
    public string Path { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EncryptedValue { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Add EF configuration**

Create `src/libs/Brigade.Admin.Data/Configurations/SecretOptionsConfiguration.cs`:

```csharp
using Brigade.Admin.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brigade.Admin.Data.Configurations;

public class SecretOptionsConfiguration : IEntityTypeConfiguration<SecretOptions>
{
    public void Configure(EntityTypeBuilder<SecretOptions> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Path).IsRequired().HasMaxLength(500);
        builder.Property(s => s.EncryptedValue).IsRequired();
        builder.HasIndex(s => s.Path).IsUnique();
    }
}
```

- [ ] **Step 3: Add DbSet to AppDbContext**

Modify `src/libs/Brigade.Admin.Data/AppDbContext.cs`:

```csharp
using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Models.Agents;
using Brigade.Admin.Data.Models.Providers;
using Microsoft.EntityFrameworkCore;

namespace Brigade.Admin.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AgentOptions> Agents => Set<AgentOptions>();
    public DbSet<ToolOptions> Tools => Set<ToolOptions>();
    public DbSet<SkillOptions> Skills => Set<SkillOptions>();
    public DbSet<MemoryOptions> Memories => Set<MemoryOptions>();
    public DbSet<McpServerOptions> Mcps => Set<McpServerOptions>();
    public DbSet<McpServerHeaders> McpServerHeaders => Set<McpServerHeaders>();
    public DbSet<WorkflowOptions> Workflows => Set<WorkflowOptions>();
    public DbSet<ProviderOptions> Providers => Set<ProviderOptions>();
    public DbSet<ModelOptions> Models => Set<ModelOptions>();
    public DbSet<MiddlewareOptions> Middlewares => Set<MiddlewareOptions>();
    public DbSet<SecretOptions> Secrets => Set<SecretOptions>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
```

- [ ] **Step 4: Build to verify**

```bash
cd /home/timm/Brigade/.worktrees/features/secrets-management
dotnet build src/libs/Brigade.Admin.Data/Brigade.Admin.Data.csproj
```

Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add src/libs/Brigade.Admin.Data.Models/SecretOptions.cs \
        src/libs/Brigade.Admin.Data/Configurations/SecretOptionsConfiguration.cs \
        src/libs/Brigade.Admin.Data/AppDbContext.cs
git commit -m "feat: add SecretOptions model and EF configuration"
```

---

## Task 2: ISecretsStore Interface + SecretsManager Interface

**Files:**
- Create: `src/libs/Brigade.Admin.Data/Stores/SecretsStore.cs`
- Create: `src/libs/Brigade.Admin.Data/Services/SecretsManager.cs`

- [ ] **Step 1: Create ISecretsStore**

Create `src/libs/Brigade.Admin.Data/Stores/SecretsStore.cs`:

```csharp
using Brigade.Admin.Data.Models;

namespace Brigade.Admin.Data.Stores;

public interface ISecretsStore
{
    Task<List<SecretOptions>> GetAllAsync(CancellationToken ct = default);
    Task<SecretOptions?> FindByPathAsync(string path, CancellationToken ct = default);
    Task<List<SecretOptions>> GetChildrenAsync(string parentPath, CancellationToken ct = default);
    Task<SecretOptions> CreateAsync(string path, string? description, string plaintext, CancellationToken ct = default);
    Task UpdateValueAsync(int id, string plaintext, CancellationToken ct = default);
    Task UpdateDescriptionAsync(int id, string? description, CancellationToken ct = default);
    Task<string?> DecryptAsync(int id, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}
```

- [ ] **Step 2: Create ISecretsManager and SecretRequest**

Create `src/libs/Brigade.Admin.Data/Services/SecretsManager.cs`:

```csharp
using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Stores;
using System.Text.Json;
using System.Web;

namespace Brigade.Admin.Data.Services;

public record SecretRequest
{
    public string Path { get; init; } = string.Empty;
    public string MediaType { get; init; } = "text/plain";
}

public interface ISecretsManager
{
    bool IsSecretReference(string? value);
    Task<string?> ResolveAsync(SecretRequest request, CancellationToken ct = default);
}

public class SecretsManager(ISecretsStore store) : ISecretsManager
{
    private const string Scheme = "secret://";

    public bool IsSecretReference(string? value) =>
        value?.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase) == true;

    public async Task<string?> ResolveAsync(SecretRequest request, CancellationToken ct = default)
    {
        var path = request.Path.StartsWith(Scheme, StringComparison.OrdinalIgnoreCase)
            ? request.Path[Scheme.Length..]
            : request.Path;

        var leaf = await store.FindByPathAsync(path, ct);
        if (leaf is not null)
        {
            var value = await store.DecryptAsync(leaf.Id, ct);
            if (request.MediaType == "text/json")
            {
                var key = path.Split('/').Last();
                return JsonSerializer.Serialize(new { key, value });
            }
            return value;
        }

        var children = await store.GetChildrenAsync(path, ct);
        if (children.Count == 0) return null;

        var pairs = new List<(string key, string? val)>();
        foreach (var child in children)
        {
            var childKey = child.Path.Split('/').Last();
            var childValue = await store.DecryptAsync(child.Id, ct);
            pairs.Add((childKey, childValue));
        }

        if (request.MediaType == "text/json")
        {
            var items = pairs.Select(p => new { key = p.key, value = p.val });
            return JsonSerializer.Serialize(items);
        }

        return string.Join("&", pairs.Select(p => $"{HttpUtility.UrlEncode(p.key)}={HttpUtility.UrlEncode(p.val)}"));
    }
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build src/libs/Brigade.Admin.Data/Brigade.Admin.Data.csproj
```

Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add src/libs/Brigade.Admin.Data/Stores/SecretsStore.cs \
        src/libs/Brigade.Admin.Data/Services/SecretsManager.cs
git commit -m "feat: add ISecretsStore interface and SecretsManager service"
```

---

## Task 3: Test Project + SecretsManager Tests

**Files:**
- Create: `tests/Brigade.Secrets.Tests/Brigade.Secrets.Tests.csproj`
- Create: `tests/Brigade.Secrets.Tests/SecretsManagerTests.cs`

- [ ] **Step 1: Create test project**

```bash
dotnet new xunit -n Brigade.Secrets.Tests -o tests/Brigade.Secrets.Tests --framework net10.0
dotnet sln Brigade.slnx add tests/Brigade.Secrets.Tests/Brigade.Secrets.Tests.csproj
```

- [ ] **Step 2: Add project reference to Brigade.Admin.Data**

Edit `tests/Brigade.Secrets.Tests/Brigade.Secrets.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\libs\Brigade.Admin.Data\Brigade.Admin.Data.csproj" />
  </ItemGroup>
</Project>
```

Add `NSubstitute` to `Directory.Packages.props`:

```xml
<PackageVersion Include="NSubstitute" Version="5.3.0" />
```

- [ ] **Step 3: Write failing tests for SecretsManager**

Create `tests/Brigade.Secrets.Tests/SecretsManagerTests.cs`:

```csharp
using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Services;
using Brigade.Admin.Data.Stores;
using NSubstitute;
using Xunit;

namespace Brigade.Secrets.Tests;

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
        var leaf = new SecretOptions { Id = 1, Path = "openai/key" };
        _store.FindByPathAsync("openai/key").Returns(leaf);
        _store.DecryptAsync(1).Returns("sk-secret-value");

        var result = await _sut.ResolveAsync(new SecretRequest { Path = "secret://openai/key" });

        Assert.Equal("sk-secret-value", result);
    }

    [Fact]
    public async Task ResolveAsync_LeafPath_TextJson_ReturnsJsonObject()
    {
        var leaf = new SecretOptions { Id = 1, Path = "openai/client_ids/test" };
        _store.FindByPathAsync("openai/client_ids/test").Returns(leaf);
        _store.DecryptAsync(1).Returns("foo");

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
        _store.GetChildrenAsync("openai/client_ids").Returns([
            new SecretOptions { Id = 1, Path = "openai/client_ids/test" },
            new SecretOptions { Id = 2, Path = "openai/client_ids/prod" }
        ]);
        _store.DecryptAsync(1).Returns("foo");
        _store.DecryptAsync(2).Returns("bar");

        var result = await _sut.ResolveAsync(new SecretRequest { Path = "secret://openai/client_ids" });

        Assert.Equal("test=foo&prod=bar", result);
    }

    [Fact]
    public async Task ResolveAsync_CollectionPath_TextJson_ReturnsJsonArray()
    {
        _store.FindByPathAsync("openai/client_ids").Returns((SecretOptions?)null);
        _store.GetChildrenAsync("openai/client_ids").Returns([
            new SecretOptions { Id = 1, Path = "openai/client_ids/test" }
        ]);
        _store.DecryptAsync(1).Returns("foo");

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
```

- [ ] **Step 4: Run tests to verify they fail**

```bash
dotnet test tests/Brigade.Secrets.Tests/ --filter "SecretsManagerTests" -v minimal
```

Expected: FAIL — `NSubstitute` not found or compilation errors (project not yet built end-to-end)

- [ ] **Step 5: Restore and run again**

```bash
dotnet restore tests/Brigade.Secrets.Tests/
dotnet test tests/Brigade.Secrets.Tests/ --filter "SecretsManagerTests" -v minimal
```

Expected: All 8 tests PASS (SecretsManager is already implemented in Task 2)

- [ ] **Step 6: Commit**

```bash
git add tests/Brigade.Secrets.Tests/ Directory.Packages.props Brigade.slnx
git commit -m "test: add SecretsManager unit tests"
```

---

## Task 4: SqliteSecretsStore

**Files:**
- Create: `src/libs/Brigade.Admin.Data.Sqlite/Stores/SqliteSecretsStore.cs`

- [ ] **Step 1: Write failing store integration tests**

Create `tests/Brigade.Secrets.Tests/SqliteSecretsStoreTests.cs`:

```csharp
using Brigade.Admin.Data;
using Brigade.Admin.Data.Sqlite.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Brigade.Secrets.Tests;

public class SqliteSecretsStoreTests : IDisposable
{
    private readonly AppDbContext _db;
    private readonly SqliteSecretsStore _sut;

    public SqliteSecretsStoreTests()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
        _sut = new SqliteSecretsStore(_db);
    }

    public void Dispose() => _db.Dispose();

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
```

Add project reference to `Brigade.Admin.Data.Sqlite` in test csproj:

```xml
<ProjectReference Include="..\..\src\libs\Brigade.Admin.Data.Sqlite\Brigade.Admin.Data.Sqlite.csproj" />
```

Also add to `Directory.Packages.props` if not already present:

```xml
<PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.0" />
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Brigade.Secrets.Tests/ --filter "SqliteSecretsStoreTests" -v minimal
```

Expected: FAIL — `SqliteSecretsStore` does not exist

- [ ] **Step 3: Implement SqliteSecretsStore**

Create `src/libs/Brigade.Admin.Data.Sqlite/Stores/SqliteSecretsStore.cs`:

```csharp
using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace Brigade.Admin.Data.Sqlite.Stores;

public class SqliteSecretsStore(AppDbContext db) : ISecretsStore
{
    public async Task<List<SecretOptions>> GetAllAsync(CancellationToken ct = default) =>
        await db.Secrets.AsNoTracking().OrderBy(s => s.Path).ToListAsync(ct);

    public async Task<SecretOptions?> FindByPathAsync(string path, CancellationToken ct = default) =>
        await db.Secrets.AsNoTracking().FirstOrDefaultAsync(s => s.Path == path, ct);

    public async Task<List<SecretOptions>> GetChildrenAsync(string parentPath, CancellationToken ct = default)
    {
        var prefix = parentPath.TrimEnd('/') + "/";
        return await db.Secrets.AsNoTracking()
            .Where(s => s.Path.StartsWith(prefix)
                     && !s.Path[prefix.Length..].Contains('/'))
            .ToListAsync(ct);
    }

    public async Task<SecretOptions> CreateAsync(string path, string? description, string plaintext, CancellationToken ct = default)
    {
        var secret = new SecretOptions
        {
            Path = path,
            Description = description,
            EncryptedValue = plaintext,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Secrets.Add(secret);
        await db.SaveChangesAsync(ct);
        return secret;
    }

    public async Task UpdateValueAsync(int id, string plaintext, CancellationToken ct = default)
    {
        var secret = await db.Secrets.FindAsync([id], ct);
        if (secret is null) return;
        secret.EncryptedValue = plaintext;
        secret.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateDescriptionAsync(int id, string? description, CancellationToken ct = default)
    {
        var secret = await db.Secrets.FindAsync([id], ct);
        if (secret is null) return;
        secret.Description = description;
        secret.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public Task<string?> DecryptAsync(int id, CancellationToken ct = default) =>
        db.Secrets.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => (string?)s.EncryptedValue)
            .FirstOrDefaultAsync(ct);

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var secret = await db.Secrets.FindAsync([id], ct);
        if (secret is not null)
        {
            db.Secrets.Remove(secret);
            await db.SaveChangesAsync(ct);
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Brigade.Secrets.Tests/ --filter "SqliteSecretsStoreTests" -v minimal
```

Expected: All 7 tests PASS

- [ ] **Step 5: Commit**

```bash
git add src/libs/Brigade.Admin.Data.Sqlite/Stores/SqliteSecretsStore.cs \
        tests/Brigade.Secrets.Tests/SqliteSecretsStoreTests.cs \
        tests/Brigade.Secrets.Tests/Brigade.Secrets.Tests.csproj
git commit -m "feat: add SqliteSecretsStore with plaintext storage"
```

---

## Task 5: PostgreSqlSecretsStore

**Files:**
- Create: `src/libs/Brigade.Admin.Data.PostgreSQL/Stores/PostgreSqlSecretsStore.cs`

The PostgreSQL store uses `pgp_sym_encrypt` and `pgp_sym_decrypt` from the `pgcrypto` extension. It uses raw SQL for all encrypt/decrypt operations since EF Core cannot translate these functions. The encryption key comes from `IConfiguration["Secrets:EncryptionKey"]`.

> **Note:** Unit tests for this store require a live PostgreSQL instance with `pgcrypto` enabled. No xUnit tests are written here — verify via manual integration testing after migrations run.

- [ ] **Step 1: Implement PostgreSqlSecretsStore**

Create `src/libs/Brigade.Admin.Data.PostgreSQL/Stores/PostgreSqlSecretsStore.cs`:

```csharp
using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Brigade.Admin.Data.PostgreSQL.Stores;

public class PostgreSqlSecretsStore(AppDbContext db, IConfiguration config) : ISecretsStore
{
    private string EncryptionKey => config["Secrets:EncryptionKey"]
        ?? throw new InvalidOperationException("Secrets:EncryptionKey is not configured.");

    public async Task<List<SecretOptions>> GetAllAsync(CancellationToken ct = default) =>
        await db.Secrets.AsNoTracking().OrderBy(s => s.Path).ToListAsync(ct);

    public async Task<SecretOptions?> FindByPathAsync(string path, CancellationToken ct = default) =>
        await db.Secrets.AsNoTracking().FirstOrDefaultAsync(s => s.Path == path, ct);

    public async Task<List<SecretOptions>> GetChildrenAsync(string parentPath, CancellationToken ct = default)
    {
        var prefix = parentPath.TrimEnd('/') + "/";
        var prefixPattern = prefix + "%";
        var nestedPattern = prefix + "%/%";
        return await db.Secrets.AsNoTracking()
            .Where(s => EF.Functions.Like(s.Path, prefixPattern)
                     && !EF.Functions.Like(s.Path, nestedPattern))
            .ToListAsync(ct);
    }

    public async Task<SecretOptions> CreateAsync(string path, string? description, string plaintext, CancellationToken ct = default)
    {
        var key = EncryptionKey;
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Secrets" ("Path", "Description", "EncryptedValue", "CreatedAt", "UpdatedAt")
            VALUES ({0}, {1}, pgp_sym_encrypt({2}, {3})::text, NOW(), NOW())
            """,
            [path, description as object ?? DBNull.Value, plaintext, key], ct);

        return await db.Secrets.AsNoTracking().FirstAsync(s => s.Path == path, ct);
    }

    public async Task UpdateValueAsync(int id, string plaintext, CancellationToken ct = default)
    {
        var key = EncryptionKey;
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE "Secrets" SET "EncryptedValue" = pgp_sym_encrypt({0}, {1})::text, "UpdatedAt" = NOW()
            WHERE "Id" = {2}
            """,
            [plaintext, key, id], ct);
    }

    public async Task UpdateDescriptionAsync(int id, string? description, CancellationToken ct = default)
    {
        var secret = await db.Secrets.FindAsync([id], ct);
        if (secret is null) return;
        secret.Description = description;
        secret.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> DecryptAsync(int id, CancellationToken ct = default)
    {
        var key = EncryptionKey;
        var results = await db.Database
            .SqlQueryRaw<string>(
                """SELECT pgp_sym_decrypt("EncryptedValue"::bytea, {0}) AS "Value" FROM "Secrets" WHERE "Id" = {1}""",
                key, id)
            .ToListAsync(ct);
        return results.FirstOrDefault();
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var secret = await db.Secrets.FindAsync([id], ct);
        if (secret is not null)
        {
            db.Secrets.Remove(secret);
            await db.SaveChangesAsync(ct);
        }
    }
}
```

- [ ] **Step 2: Build to verify**

```bash
dotnet build src/libs/Brigade.Admin.Data.PostgreSQL/Brigade.Admin.Data.PostgreSQL.csproj
```

Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add src/libs/Brigade.Admin.Data.PostgreSQL/Stores/PostgreSqlSecretsStore.cs
git commit -m "feat: add PostgreSqlSecretsStore with pgcrypto encryption"
```

---

## Task 6: EF Core Migrations

**Files:**
- Create: migrations in `src/libs/Brigade.Admin.Data.Sqlite/Migrations/`
- Create: migrations in `src/libs/Brigade.Admin.Data.PostgreSQL/Migrations/AgentDb/`

The `DesignTimeFactory` classes already exist in each project and will be used by `dotnet ef`.

- [ ] **Step 1: Generate SQLite migration**

```bash
dotnet ef migrations add AddSecrets \
  --project src/libs/Brigade.Admin.Data.Sqlite \
  --startup-project src/WebDev \
  --context AppDbContext \
  --output-dir Migrations
```

Expected: Migration file created at `src/libs/Brigade.Admin.Data.Sqlite/Migrations/<timestamp>_AddSecrets.cs`

- [ ] **Step 2: Verify SQLite migration content**

Open the generated migration file and verify it contains:

```csharp
migrationBuilder.CreateTable(
    name: "Secrets",
    columns: table => new
    {
        Id = table.Column<int>(type: "INTEGER", nullable: false)
            .Annotation("Sqlite:Autoincrement", true),
        Path = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
        Description = table.Column<string>(type: "TEXT", nullable: true),
        EncryptedValue = table.Column<string>(type: "TEXT", nullable: false),
        CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
        UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
    },
    // ...
```

- [ ] **Step 3: Generate PostgreSQL migration**

```bash
dotnet ef migrations add AddSecrets \
  --project src/libs/Brigade.Admin.Data.PostgreSQL \
  --startup-project src/WebDev \
  --context AppDbContext \
  --output-dir Migrations/AgentDb
```

Expected: Migration file created at `src/libs/Brigade.Admin.Data.PostgreSQL/Migrations/AgentDb/<timestamp>_AddSecrets.cs`

- [ ] **Step 4: Enable pgcrypto in PostgreSQL migration**

Open the generated PostgreSQL migration and add pgcrypto setup to `Up`:

```csharp
public partial class AddSecrets : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");

        migrationBuilder.CreateTable(
            name: "Secrets",
            // ... (keep generated columns as-is)
        );
        // keep generated index
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "Secrets");
        // Do NOT drop pgcrypto — it may be used by other things
    }
}
```

- [ ] **Step 5: Apply migrations to SQLite dev database**

```bash
cd src/WebDev && dotnet run -- --migrate-only 2>/dev/null || true
```

If the above doesn't work (there's no `--migrate-only` flag), just run the app briefly and check that the `Secrets` table exists:

```bash
cd src/WebDev
dotnet ef database update \
  --project ../libs/Brigade.Admin.Data.Sqlite \
  --context AppDbContext \
  --connection "Data Source=agent.db"
```

- [ ] **Step 6: Commit**

```bash
git add src/libs/Brigade.Admin.Data.Sqlite/Migrations/ \
        src/libs/Brigade.Admin.Data.PostgreSQL/Migrations/AgentDb/
git commit -m "feat: add EF migrations for Secrets table"
```

---

## Task 7: DI Registration

**Files:**
- Modify: `src/libs/Brigade.Admin.Data/Extensions/AgentDbContextExtensions.cs`
- Modify: `src/libs/Brigade.Admin.Data.Sqlite/Extensions/SqliteExtensions.cs`
- Modify: `src/libs/Brigade.Admin.Data.PostgreSQL/Extensions/PostgreSqlExtensions.cs`

- [ ] **Step 1: Register ISecretsManager in AgentDbContextExtensions**

Modify `src/libs/Brigade.Admin.Data/Extensions/AgentDbContextExtensions.cs`:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Brigade.Admin.Data.Stores;
using Brigade.Admin.Data.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;

namespace Brigade.Admin.Data;

public static partial class Extensions
{
    public static IServiceCollection AddInMemoryAgentDbContext(this IServiceCollection services)
    {
        return services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("AgentDb"));
    }

    public static IServiceCollection AddAgentStores(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<AgentStore>();
        services.AddScoped<ToolStore>();
        services.AddScoped<SkillStore>();
        services.AddScoped<MemoryStore>();
        services.AddScoped<McpStore>();
        services.AddScoped<WorkflowStore>();
        services.AddScoped<ProviderStore>();
        services.AddScoped<ModelStore>();
        services.AddScoped<MiddlewareStore>();
        services.AddScoped<ISecretsManager, SecretsManager>();
        return services;
    }

    public static async Task DoAgentDbMigration(this WebApplication app)
    {
        using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}
```

- [ ] **Step 2: Register SqliteSecretsStore in SqliteExtensions**

Modify `src/libs/Brigade.Admin.Data.Sqlite/Extensions/SqliteExtensions.cs`:

```csharp
using Brigade.Admin.Data.Sqlite.Stores;
using Brigade.Admin.Data.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brigade.Admin.Data;

public static class SqliteExtensions
{
    public static IServiceCollection AddSqliteAppDbContext(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<ISecretsStore, SqliteSecretsStore>();
        return services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString, o => o.MigrationsAssembly("Brigade.Admin.Data.Sqlite")));
    }
}
```

- [ ] **Step 3: Register PostgreSqlSecretsStore in PostgreSqlExtensions**

Modify `src/libs/Brigade.Admin.Data.PostgreSQL/Extensions/PostgreSqlExtensions.cs`:

```csharp
using Brigade.Admin.Data.PostgreSQL.Stores;
using Brigade.Admin.Data.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brigade.Admin.Data;

public static class PostgreSqlExtensions
{
    public static IServiceCollection AddPostgreSqlAppDbContext(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<ISecretsStore, PostgreSqlSecretsStore>();
        return services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.MigrationsAssembly("Brigade.Admin.Data.PostgreSQL")));
    }
}
```

- [ ] **Step 4: Build the full solution**

```bash
dotnet build Brigade.slnx
```

Expected: Build succeeded, 0 errors

- [ ] **Step 5: Commit**

```bash
git add src/libs/Brigade.Admin.Data/Extensions/AgentDbContextExtensions.cs \
        src/libs/Brigade.Admin.Data.Sqlite/Extensions/SqliteExtensions.cs \
        src/libs/Brigade.Admin.Data.PostgreSQL/Extensions/PostgreSqlExtensions.cs
git commit -m "feat: register ISecretsStore and ISecretsManager in DI"
```

---

## Task 8: SecretResolvingHandler + AgentFactory Update

**Files:**
- Create: `src/Brigade.Agents/Http/SecretResolvingHandler.cs`
- Modify: `src/Brigade.Agents/Brigade.Agents.csproj`
- Modify: `src/Brigade.Agents/Providers/AgentFactory.cs`
- Create: `tests/Brigade.Secrets.Tests/SecretResolvingHandlerTests.cs`

- [ ] **Step 1: Write failing handler tests**

Create `tests/Brigade.Secrets.Tests/SecretResolvingHandlerTests.cs`:

```csharp
using Brigade.Admin.Data.Services;
using Brigade.Agents.Http;
using NSubstitute;
using System.Net;
using Xunit;

namespace Brigade.Secrets.Tests;

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
        _secrets.ResolveAsync(Arg.Any<SecretRequest>()).Returns("sk-resolved");

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
```

Add project reference in `Brigade.Secrets.Tests.csproj`:

```xml
<ProjectReference Include="..\..\src\Brigade.Agents\Brigade.Agents.csproj" />
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Brigade.Secrets.Tests/ --filter "SecretResolvingHandlerTests" -v minimal
```

Expected: FAIL — `SecretResolvingHandler` does not exist

- [ ] **Step 3: Add project reference to Brigade.Agents.csproj**

Modify `src/Brigade.Agents/Brigade.Agents.csproj`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <!-- <PackageReference Include="Microsoft.Extensions.AI.Ollama" Version="9.7.0-preview.1.25356.2" /> -->
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="Anthropic" />
    <PackageReference Include="Microsoft.Agents.AI" />
    <PackageReference Include="Microsoft.Agents.AI.Abstractions" />
    <PackageReference Include="Microsoft.Agents.AI.Anthropic" />
    <PackageReference Include="Microsoft.Agents.AI.GitHub.Copilot" />
    <PackageReference Include="Microsoft.Agents.AI.OpenAI" />
    <PackageReference Include="Microsoft.Extensions.AI" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\libs\Brigade.Admin.Data\Brigade.Admin.Data.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create SecretResolvingHandler**

Create `src/Brigade.Agents/Http/SecretResolvingHandler.cs`:

```csharp
using Brigade.Admin.Data.Services;

namespace Brigade.Agents.Http;

public class SecretResolvingHandler(ISecretsManager secrets) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        foreach (var (key, values) in request.Headers.ToList())
        {
            var resolved = new List<string>();
            foreach (var v in values)
            {
                resolved.Add(secrets.IsSecretReference(v)
                    ? await secrets.ResolveAsync(new SecretRequest { Path = v }, ct) ?? v
                    : v);
            }
            request.Headers.Remove(key);
            request.Headers.TryAddWithoutValidation(key, resolved);
        }
        return await base.SendAsync(request, ct);
    }
}
```

- [ ] **Step 5: Update AgentFactory to resolve ApiToken**

Modify `src/Brigade.Agents/Providers/AgentFactory.cs`:

```csharp
using Brigade.Admin.Data.Services;
using Microsoft.Agents.AI;

namespace Brigade.Agents.Providers;

public sealed class AgentFactory(ISecretsManager secrets)
{
    public async Task<AIAgent> CreateAgentAsync(AgentOptions options, CancellationToken ct = default)
    {
        if (secrets.IsSecretReference(options.ApiToken))
        {
            options = options with
            {
                ApiToken = await secrets.ResolveAsync(
                    new SecretRequest { Path = options.ApiToken! }, ct)
            };
        }

        return Enum.Parse<ProvidersEnum>(options.Provider ?? "UNSUPPORTED") switch
        {
            ProvidersEnum.MiniMax   => new MiniMaxProvider().CreateAgent(options),
            ProvidersEnum.Anthropic => new AnthropicProvider().CreateAgent(options),
            _ => throw new NotSupportedException($"The provider {options.Provider} is not supported.")
        };
    }
}
```

> **Note:** `AgentOptions` is defined in `Brigade.Agents`. Check whether it has an `ApiToken` property — if not, verify the actual property name by reading `src/Brigade.Agents/Providers/AgentOptions.cs` and adjust accordingly.

- [ ] **Step 6: Run handler tests to verify they pass**

```bash
dotnet test tests/Brigade.Secrets.Tests/ --filter "SecretResolvingHandlerTests" -v minimal
```

Expected: All 2 tests PASS

- [ ] **Step 7: Build Brigade.Agents**

```bash
dotnet build src/Brigade.Agents/Brigade.Agents.csproj
```

Expected: Build succeeded

- [ ] **Step 8: Commit**

```bash
git add src/Brigade.Agents/Http/SecretResolvingHandler.cs \
        src/Brigade.Agents/Brigade.Agents.csproj \
        src/Brigade.Agents/Providers/AgentFactory.cs \
        tests/Brigade.Secrets.Tests/SecretResolvingHandlerTests.cs
git commit -m "feat: add SecretResolvingHandler and update AgentFactory to resolve secrets"
```

---

## Task 9: SecretsController (Reveal Endpoint)

**Files:**
- Create: `src/WebDev/Controllers/SecretsController.cs`
- Create: `tests/Brigade.Secrets.Tests/SecretsControllerTests.cs`

- [ ] **Step 1: Write failing controller tests**

Create `tests/Brigade.Secrets.Tests/SecretsControllerTests.cs`:

```csharp
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
```

Add `WebDev` project reference to test csproj:

```xml
<ProjectReference Include="..\..\src\WebDev\WebDev.csproj" />
```

- [ ] **Step 2: Run tests to verify they fail**

```bash
dotnet test tests/Brigade.Secrets.Tests/ --filter "SecretsControllerTests" -v minimal
```

Expected: FAIL — `SecretsController` does not exist

- [ ] **Step 3: Create SecretsController**

Create `src/WebDev/Controllers/SecretsController.cs`:

```csharp
using Brigade.Admin.Data.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebDev.Controllers;

[ApiController, Route("api/secrets")]
[Authorize(Roles = "admin")]
public class SecretsController(ISecretsStore store) : ControllerBase
{
    [HttpPost("{id:int}/reveal")]
    public async Task<IActionResult> Reveal(int id, CancellationToken ct = default)
    {
        var value = await store.DecryptAsync(id, ct);
        if (value is null) return NotFound();
        return Ok(new { value });
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

```bash
dotnet test tests/Brigade.Secrets.Tests/ --filter "SecretsControllerTests" -v minimal
```

Expected: All 2 tests PASS

- [ ] **Step 5: Commit**

```bash
git add src/WebDev/Controllers/SecretsController.cs \
        tests/Brigade.Secrets.Tests/SecretsControllerTests.cs
git commit -m "feat: add SecretsController with reveal endpoint"
```

---

## Task 10: Secrets.razor UI Page

**Files:**
- Create: `src/WebDev/Components/Pages/Secrets.razor`

This is a Blazor component. TDD does not apply — verify by running the dev server and navigating to `/admin/secrets`.

> **Insight:** The `SecretTreeNode` tree is built client-side by splitting each `SecretOptions.Path` on `/`. Folder nodes are synthetic — they have no `Id`. Only leaf nodes (those with an exact `Path` match in the database) are actual `SecretOptions` records.

- [ ] **Step 1: Create Secrets.razor**

Create `src/WebDev/Components/Pages/Secrets.razor`:

```razor
@page "/admin/secrets"
@layout Brigade.Admin.UI.Layouts.MainLayout
@rendermode InteractiveServer
@attribute [Authorize]
@inject ISecretsStore SecretsStore
@inject NotificationService NotificationService
@inject ContextMenuService ContextMenuService
@inject DialogService DialogService

<PageTitle>Secrets</PageTitle>

<RadzenText TextStyle="TextStyle.H4" TagName="TagName.H1" class="rz-mb-4">Secrets</RadzenText>

<RadzenRow>
    <RadzenColumn Size="4">
        <RadzenTextBox Placeholder="Search…" @oninput="OnSearch" style="width: 100%; margin-bottom: 0.5rem;" />
        <RadzenTree Data="@rootNodes" @bind-Value="selected"
                    Expand="OnExpand"
                    ItemRender="OnItemRender"
                    Style="width: 100%; height: 600px; overflow: auto; border: 1px solid var(--rz-base-300);">
            <RadzenTreeLevel TextProperty="Label" HasChildren="@(n => ((SecretTreeNode)n).IsFolder)" />
        </RadzenTree>
    </RadzenColumn>
    <RadzenColumn Size="8">
        @if (selected is SecretTreeNode { IsFolder: false } leaf && leaf.Secret is not null)
        {
            <RadzenCard>
                <RadzenStack Orientation="Orientation.Vertical" Gap="0.5rem">
                    <RadzenText TextStyle="TextStyle.Subtitle1">@($"secret://{leaf.Secret.Path}")</RadzenText>
                    <RadzenText TextStyle="TextStyle.Body2">@(leaf.Secret.Description ?? "No description")</RadzenText>
                    <RadzenText>Value: <code>@(revealedValue ?? "•••••••")</code></RadzenText>
                    @if (revealedValue is not null)
                    {
                        <RadzenText TextStyle="TextStyle.Caption">Hides in @revealCountdown seconds</RadzenText>
                    }
                    <RadzenStack Orientation="Orientation.Horizontal">
                        <RadzenButton Text="Reveal" Icon="visibility" Click="RevealSelected"
                                      Disabled="@(revealedValue is not null)" />
                        <RadzenButton Text="Edit Description" Icon="edit" ButtonStyle="ButtonStyle.Light"
                                      Click="EditDescription" />
                        <RadzenButton Text="Replace Value" Icon="lock_reset" ButtonStyle="ButtonStyle.Light"
                                      Click="ReplaceValue" />
                        <RadzenButton Text="Delete" Icon="delete" ButtonStyle="ButtonStyle.Danger"
                                      Click="DeleteSelected" />
                    </RadzenStack>
                </RadzenStack>
            </RadzenCard>
        }
        else if (selected is SecretTreeNode { IsFolder: true } folder)
        {
            <RadzenCard>
                <RadzenText TextStyle="TextStyle.Subtitle1">Folder: @folder.Label</RadzenText>
                <RadzenButton Text="Add secret here" Icon="add" Click="@(() => AddSecretAt(folder.FullPath))" />
            </RadzenCard>
        }
    </RadzenColumn>
</RadzenRow>

@code {
    private List<SecretOptions> allSecrets = [];
    private List<SecretTreeNode> rootNodes = [];
    private object? selected;
    private string? revealedValue;
    private int revealCountdown;
    private System.Timers.Timer? revealTimer;
    private string searchTerm = string.Empty;

    protected override async Task OnInitializedAsync() => await LoadAsync();

    private async Task LoadAsync()
    {
        allSecrets = await SecretsStore.GetAllAsync();
        rootNodes = BuildTree(allSecrets, searchTerm);
    }

    private static List<SecretTreeNode> BuildTree(IEnumerable<SecretOptions> secrets, string filter)
    {
        var filtered = string.IsNullOrWhiteSpace(filter)
            ? secrets
            : secrets.Where(s => s.Path.Contains(filter, StringComparison.OrdinalIgnoreCase));

        var root = new Dictionary<string, SecretTreeNode>(StringComparer.OrdinalIgnoreCase);

        foreach (var secret in filtered)
        {
            var parts = secret.Path.Split('/');
            var currentDict = root;
            string currentPath = string.Empty;

            for (int i = 0; i < parts.Length; i++)
            {
                currentPath = i == 0 ? parts[i] : $"{currentPath}/{parts[i]}";
                if (!currentDict.TryGetValue(parts[i], out var node))
                {
                    node = new SecretTreeNode
                    {
                        Label = parts[i],
                        FullPath = currentPath,
                        IsFolder = i < parts.Length - 1,
                        Secret = i == parts.Length - 1 ? secret : null,
                        Children = []
                    };
                    currentDict[parts[i]] = node;
                }
                currentDict = node.Children.ToDictionary(c => c.Label, StringComparer.OrdinalIgnoreCase);
            }
        }

        return root.Values.OrderBy(n => n.Label).ToList();
    }

    private Task OnExpand(TreeExpandEventArgs args)
    {
        if (args.Value is SecretTreeNode node)
            args.Children.Data = node.Children;
        return Task.CompletedTask;
    }

    private void OnItemRender(TreeItemRenderEventArgs args)
    {
        if (args.Value is SecretTreeNode { IsFolder: true })
            args.Item.Icon = "folder";
        else
            args.Item.Icon = "key";
    }

    private void OnSearch(ChangeEventArgs e)
    {
        searchTerm = e.Value?.ToString() ?? string.Empty;
        rootNodes = BuildTree(allSecrets, searchTerm);
    }

    private async Task RevealSelected()
    {
        if (selected is not SecretTreeNode { Secret: not null } leaf) return;
        revealedValue = await SecretsStore.DecryptAsync(leaf.Secret.Id);
        revealCountdown = 30;

        revealTimer?.Dispose();
        revealTimer = new System.Timers.Timer(1000);
        revealTimer.Elapsed += async (_, _) =>
        {
            revealCountdown--;
            if (revealCountdown <= 0)
            {
                revealedValue = null;
                revealTimer?.Dispose();
            }
            await InvokeAsync(StateHasChanged);
        };
        revealTimer.Start();
    }

    private async Task EditDescription()
    {
        if (selected is not SecretTreeNode { Secret: not null } leaf) return;
        var result = await DialogService.OpenAsync<DescriptionDialog>("Edit Description",
            new Dictionary<string, object?> { ["Current"] = leaf.Secret.Description });
        if (result is string desc)
        {
            await SecretsStore.UpdateDescriptionAsync(leaf.Secret.Id, desc);
            await LoadAsync();
        }
    }

    private async Task ReplaceValue()
    {
        if (selected is not SecretTreeNode { Secret: not null } leaf) return;
        var result = await DialogService.OpenAsync<SecretValueDialog>("Replace Value",
            new Dictionary<string, object?> { ["Path"] = leaf.Secret.Path });
        if (result is string newValue)
        {
            await SecretsStore.UpdateValueAsync(leaf.Secret.Id, newValue);
            NotificationService.Notify(NotificationSeverity.Success, "Value updated");
        }
    }

    private async Task AddSecretAt(string pathPrefix)
    {
        var result = await DialogService.OpenAsync<CreateSecretDialog>("Add Secret",
            new Dictionary<string, object?> { ["PathPrefix"] = pathPrefix });
        if (result is (string path, string? description, string value))
        {
            await SecretsStore.CreateAsync(path, description, value);
            await LoadAsync();
        }
    }

    private async Task DeleteSelected()
    {
        if (selected is not SecretTreeNode { Secret: not null } leaf) return;
        var confirmed = await DialogService.Confirm($"Delete secret '{leaf.Secret.Path}'?", "Confirm Delete");
        if (confirmed == true)
        {
            await SecretsStore.DeleteAsync(leaf.Secret.Id);
            selected = null;
            await LoadAsync();
            NotificationService.Notify(NotificationSeverity.Success, "Secret deleted");
        }
    }

    public void Dispose() => revealTimer?.Dispose();

    private sealed class SecretTreeNode
    {
        public string Label { get; init; } = string.Empty;
        public string FullPath { get; init; } = string.Empty;
        public bool IsFolder { get; init; }
        public SecretOptions? Secret { get; init; }
        public List<SecretTreeNode> Children { get; init; } = [];
    }
}
```

> **Note:** This component references `DescriptionDialog`, `SecretValueDialog`, and `CreateSecretDialog` — simple Radzen dialog components for text input. Create these as minimal inline dialogs or use `DialogService.OpenAsync` with a prompt. For a first pass, use `DialogService.OpenAsync` returning a string directly if Radzen supports it; otherwise create minimal dialog components in `src/WebDev/Components/`.

- [ ] **Step 2: Create minimal dialog components**

Create `src/WebDev/Components/CreateSecretDialog.razor`:

```razor
@inject DialogService DialogService

<RadzenStack Orientation="Orientation.Vertical" Gap="0.5rem" style="min-width: 300px;">
    <RadzenLabel Text="Path" />
    <RadzenTextBox @bind-Value="path" Placeholder="e.g. openai/client_ids/prod" style="width: 100%;" />
    <RadzenLabel Text="Description (optional)" />
    <RadzenTextBox @bind-Value="description" style="width: 100%;" />
    <RadzenLabel Text="Secret Value" />
    <RadzenPassword @bind-Value="value" style="width: 100%;" />
    <RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.End">
        <RadzenButton Text="Save" Click="Submit" />
        <RadzenButton Text="Cancel" ButtonStyle="ButtonStyle.Light" Click="@(() => DialogService.Close(null))" />
    </RadzenStack>
</RadzenStack>

@code {
    [Parameter] public string PathPrefix { get; set; } = string.Empty;
    private string path = string.Empty;
    private string? description;
    private string value = string.Empty;

    protected override void OnParametersSet() =>
        path = PathPrefix.TrimEnd('/') + "/";

    private void Submit() =>
        DialogService.Close((path.Trim('/'), description, value));
}
```

Create `src/WebDev/Components/SecretValueDialog.razor`:

```razor
@inject DialogService DialogService

<RadzenStack Orientation="Orientation.Vertical" Gap="0.5rem" style="min-width: 300px;">
    <RadzenText TextStyle="TextStyle.Body2">Path: <code>@Path</code></RadzenText>
    <RadzenPassword @bind-Value="newValue" Placeholder="New secret value" style="width: 100%;" />
    <RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.End">
        <RadzenButton Text="Save" Click="@(() => DialogService.Close(newValue))" />
        <RadzenButton Text="Cancel" ButtonStyle="ButtonStyle.Light" Click="@(() => DialogService.Close(null))" />
    </RadzenStack>
</RadzenStack>

@code {
    [Parameter] public string Path { get; set; } = string.Empty;
    private string newValue = string.Empty;
}
```

Create `src/WebDev/Components/DescriptionDialog.razor`:

```razor
@inject DialogService DialogService

<RadzenStack Orientation="Orientation.Vertical" Gap="0.5rem" style="min-width: 300px;">
    <RadzenTextBox @bind-Value="description" Placeholder="Description" style="width: 100%;" />
    <RadzenStack Orientation="Orientation.Horizontal" JustifyContent="JustifyContent.End">
        <RadzenButton Text="Save" Click="@(() => DialogService.Close(description))" />
        <RadzenButton Text="Cancel" ButtonStyle="ButtonStyle.Light" Click="@(() => DialogService.Close(null))" />
    </RadzenStack>
</RadzenStack>

@code {
    [Parameter] public string? Current { get; set; }
    private string? description;
    protected override void OnParametersSet() => description = Current;
}
```

- [ ] **Step 3: Build to verify**

```bash
dotnet build src/WebDev/WebDev.csproj
```

Expected: Build succeeded

- [ ] **Step 4: Run dev server and navigate to /admin/secrets**

```bash
cd src/WebDev && dotnet run
```

Navigate to `https://localhost:<port>/admin/secrets`. Verify:
- Left panel shows tree (empty initially — add a secret to test)
- Adding a secret creates a leaf node
- Reveal shows the value for 30 seconds
- Delete removes the entry

- [ ] **Step 5: Commit**

```bash
git add src/WebDev/Components/Pages/Secrets.razor \
        src/WebDev/Components/CreateSecretDialog.razor \
        src/WebDev/Components/SecretValueDialog.razor \
        src/WebDev/Components/DescriptionDialog.razor
git commit -m "feat: add Secrets management UI page with tree view"
```

---

## Task 11: Sidebar Nav + Provider/MCP Form Secret Mode Toggles

**Files:**
- Modify: `src/libs/Brigade.Admin.UI/Components/Sidebar.razor`
- Modify: `src/libs/Brigade.Admin.UI/Pages/Providers/Providers.Update.razor`
- Modify: `src/libs/Brigade.Admin.UI/Pages/Mcps/Mcps.Update.razor`

- [ ] **Step 1: Add Secrets to sidebar**

Modify `src/libs/Brigade.Admin.UI/Components/Sidebar.razor` — add after the Workflows item:

```razor
<RadzenPanelMenuItem Text="Secrets" Icon="lock" Path="/admin/secrets" />
```

Full updated PanelMenu content:
```razor
<RadzenPanelMenuItem Text="Agent Management" Icon="smart_toy">
    <RadzenPanelMenuItem Text="Dashboard" Icon="dashboard" Path="/" />
    <RadzenPanelMenuItem Text="Providers" Icon="dns" Path="/providers" />
    <RadzenPanelMenuItem Text="Agents" Icon="smart_toy" Path="/agents" />
    <RadzenPanelMenuItem Text="Models" Icon="model_training" Path="/models" />
    <RadzenPanelMenuItem Text="Tools" Icon="build" Path="/tools" />
    <RadzenPanelMenuItem Text="Skills" Icon="psychology" Path="/skills" />
    <RadzenPanelMenuItem Text="Memory" Icon="memory" Path="/memory" />
    <RadzenPanelMenuItem Text="MCP Servers" Icon="cloud" Path="/mcps" />
    <RadzenPanelMenuItem Text="Middleware" Icon="tune" Path="/middleware" />
    <RadzenPanelMenuItem Text="Workflows" Icon="account_tree" Path="/workflows" />
    <RadzenPanelMenuItem Text="Secrets" Icon="lock" Path="/admin/secrets" />
</RadzenPanelMenuItem>
```

- [ ] **Step 2: Add secret mode toggle to Providers.Update.razor**

In `src/libs/Brigade.Admin.UI/Pages/Providers/Providers.Update.razor`, replace the `ApiToken` RadzenRow with this mode-toggle version:

```razor
<RadzenRow>
    <RadzenColumn Size="4" SizeMD="4" class="rz-text-align-start rz-text-align-md-end">
        <RadzenLabel Text="API Token" />
    </RadzenColumn>
    <RadzenColumn Size="8" SizeMD="8" Style="display: block; width: 100%;">
        <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center" Gap="0.5rem" class="rz-mb-1">
            <RadzenToggleButton Text="@(apiTokenSecretMode ? "Secret" : "Raw")"
                                Icon="@(apiTokenSecretMode ? "lock" : "lock_open")"
                                Value="@apiTokenSecretMode"
                                Change="@(v => { apiTokenSecretMode = v; form.ApiToken = null; })"
                                ButtonStyle="ButtonStyle.Light" Size="ButtonSize.Small" />
        </RadzenStack>
        @if (apiTokenSecretMode)
        {
            <RadzenTextBox Name="ApiToken" @bind-Value="form.ApiToken"
                           Placeholder="secret://openai/api_key"
                           style="width: 100%;" />
        }
        else
        {
            <RadzenPassword Name="ApiToken" @bind-Value="form.ApiToken" style="width: 100%;" />
        }
    </RadzenColumn>
</RadzenRow>
```

In the `@code` block, add the toggle field after `private readonly FormModel form = new();`:

```csharp
private bool apiTokenSecretMode;
```

In `OnInitializedAsync`, detect if the stored value is a secret reference:

```csharp
form.ApiToken = provider.ApiToken;
apiTokenSecretMode = provider.ApiToken?.StartsWith("secret://") == true;
```

- [ ] **Step 3: Add secret mode toggle to Mcps.Update.razor for CommandLine**

In `src/libs/Brigade.Admin.UI/Pages/Mcps/Mcps.Update.razor`, replace the `CommandLine` RadzenRow:

```razor
<RadzenRow>
    <RadzenColumn Size="4" SizeMD="4" class="rz-text-align-start rz-text-align-md-end">
        <RadzenLabel Text="Command Line" />
    </RadzenColumn>
    <RadzenColumn Size="8" SizeMD="8" Style="display: block; width: 100%;">
        <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center" Gap="0.5rem" class="rz-mb-1">
            <RadzenToggleButton Text="@(commandLineSecretMode ? "Secret" : "Raw")"
                                Icon="@(commandLineSecretMode ? "lock" : "lock_open")"
                                Value="@commandLineSecretMode"
                                Change="@(v => { commandLineSecretMode = v; form.CommandLine = null; })"
                                ButtonStyle="ButtonStyle.Light" Size="ButtonSize.Small" />
        </RadzenStack>
        @if (commandLineSecretMode)
        {
            <RadzenTextBox Name="CommandLine" @bind-Value="form.CommandLine"
                           Placeholder="secret://mcps/myserver/command"
                           style="width: 100%;" />
        }
        else
        {
            <RadzenTextBox Name="CommandLine" @bind-Value="form.CommandLine" style="width: 100%;" />
        }
    </RadzenColumn>
</RadzenRow>
```

Add `private bool commandLineSecretMode;` to `@code`.

In `OnInitializedAsync`, add: `commandLineSecretMode = mcp.CommandLine?.StartsWith("secret://") == true;`

- [ ] **Step 4: Add secret mode toggle to HeaderEditDialog for header values**

Modify `src/libs/Brigade.Admin.UI/Components/HeaderEditDialog.razor` — in the Value input row, add a mode toggle identical to the patterns above, keyed off `headerValueSecretMode`. When in secret mode show a plain `RadzenTextBox` for the `secret://` URI; when in raw mode show the existing input.

Detect mode on open: `headerValueSecretMode = InitialValue?.StartsWith("secret://") == true;`

- [ ] **Step 5: Build Brigade.Admin.UI**

```bash
dotnet build src/libs/Brigade.Admin.UI/Brigade.Admin.UI.csproj
```

Expected: Build succeeded

- [ ] **Step 6: Run dev server and verify UI**

```bash
cd src/WebDev && dotnet run
```

Verify:
1. "Secrets" appears in left sidebar
2. Provider edit form shows toggle button for API Token — clicking it switches to a text box that accepts `secret://` URIs
3. MCP edit form shows toggle for Command Line
4. Header edit dialog shows toggle for header value
5. Saving a form with `secret://openai/key` in API Token field stores that URI string

- [ ] **Step 7: Commit**

```bash
git add src/libs/Brigade.Admin.UI/Components/Sidebar.razor \
        src/libs/Brigade.Admin.UI/Pages/Providers/Providers.Update.razor \
        src/libs/Brigade.Admin.UI/Pages/Mcps/Mcps.Update.razor \
        src/libs/Brigade.Admin.UI/Components/HeaderEditDialog.razor
git commit -m "feat: add Secrets sidebar nav and secret mode toggles in Provider/MCP forms"
```

---

## Task 12: Run All Tests + Final Build

- [ ] **Step 1: Run all tests**

```bash
dotnet test Brigade.slnx -v minimal
```

Expected: All tests PASS with no failures.

- [ ] **Step 2: Build entire solution**

```bash
dotnet build Brigade.slnx
```

Expected: Build succeeded, 0 errors, 0 warnings (or only pre-existing warnings).

- [ ] **Step 3: Smoke-test end-to-end**

1. Run `cd src/WebDev && dotnet run`
2. Navigate to `/admin/secrets`
3. Create a secret at path `openai/test-key` with value `sk-12345`
4. Navigate to `/providers`, edit a provider, switch API Token to Secret mode, enter `secret://openai/test-key`
5. Save and verify the provider record stores the `secret://` URI
6. Verify the SecretsController reveal endpoint: `POST /api/secrets/1/reveal` returns `{"value":"sk-12345"}`

- [ ] **Step 4: Final commit**

```bash
git commit --allow-empty -m "chore: complete secrets management feature implementation"
```

---

## Verification Checklist

| Area | How to verify |
|---|---|
| SecretOptions model | `dotnet build` succeeds; EF migration creates `Secrets` table |
| ISecretsStore (SQLite) | `SqliteSecretsStoreTests` all pass |
| ISecretsManager | `SecretsManagerTests` all pass |
| SecretResolvingHandler | `SecretResolvingHandlerTests` all pass |
| SecretsController reveal | `SecretsControllerTests` all pass |
| Migrations | Table exists in `agent.db` after app start |
| UI tree | Navigate to `/admin/secrets`, create/reveal/delete secrets |
| Form toggles | Provider edit stores `secret://` URI when in secret mode |
| AgentFactory | Unit test: construct factory with mock `ISecretsManager`, call `CreateAgentAsync` with `ApiToken = "secret://openai/key"`, verify mock was called |

---

## Notes for Implementer

- **NSubstitute version:** Check `Directory.Packages.props` — if `NSubstitute` isn't listed, add `<PackageVersion Include="NSubstitute" Version="5.3.0" />`.
- **AgentOptions.ApiToken:** The field name needs verification — check `src/Brigade.Agents/Providers/AgentOptions.cs` before writing `AgentFactory.cs`. The property may be named differently.
- **pgcrypto raw SQL:** The PostgreSQL store casts `pgp_sym_encrypt(...)::text` to store as `text` in EF, then casts back `::bytea` when reading. This avoids needing a `bytea` column type mapping in EF.
- **`dotnet ef` startup project:** The `--startup-project src/WebDev` is required because it references all providers and has a valid `appsettings.json` with a connection string for the migration tool.
- **Dialog components location:** The `CreateSecretDialog`, `SecretValueDialog`, and `DescriptionDialog` are in `src/WebDev/Components/`, not in `Brigade.Admin.UI`, because they're specific to this page and reference `ISecretsStore` which lives in the data layer (not the shared UI library).
