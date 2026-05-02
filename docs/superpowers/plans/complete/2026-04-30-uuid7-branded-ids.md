# UUID v7 Branded IDs Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace every `int Id` and `int XxxId` foreign key in `Legion.Admin.Data.Models` with branded `readonly record struct` types backed by UUID v7, regenerate App migrations on both PostgreSQL and SQLite, and update all dependent code (configurations, stores, controllers, Razor pages, tests).

**Architecture:** Each entity gets its own ID type (e.g., `AgentOptionsId`, `SecretOptionsId`) defined in a single `Ids.cs` file in `Legion.Admin.Data.Models`. Each ID is a `readonly record struct` wrapping a `Guid`, with implicit conversions to/from `Guid` for EF Core compatibility, and a static `New()` factory that calls `Guid.CreateVersion7()`. EF Core value converters are registered per entity in `OnModelCreating`. Auth tables (Identity + OpenIddict) are excluded — they already use string GUIDs.

**Tech Stack:** .NET 10, EF Core, PostgreSQL (Npgsql), SQLite, xUnit + NSubstitute for tests, Razor (Blazor SSR) for UI.

---

## File Structure

**Created:**
- `src/libs/Legion.Admin.Data.Models/Ids.cs` — all 11 branded ID types in one file

**Modified (model files):**
- `src/libs/Legion.Admin.Data.Models/Agents/AgentOptions.cs` — `Id`, `ProviderId`
- `src/libs/Legion.Admin.Data.Models/Agents/McpServerOptions.cs` — `McpServerOptions.Id`, `McpServerHeaders.Id`, `McpServerHeaders.McpServerId`
- `src/libs/Legion.Admin.Data.Models/Agents/MemoryOptions.cs` — `Id`, `AgentId`
- `src/libs/Legion.Admin.Data.Models/Agents/MiddlewareOptions.cs` — `Id`
- `src/libs/Legion.Admin.Data.Models/Agents/ModelOptions.cs` — `Id`
- `src/libs/Legion.Admin.Data.Models/Agents/SkillOptions.cs` — `Id`
- `src/libs/Legion.Admin.Data.Models/Agents/ToolOptions.cs` — `Id`
- `src/libs/Legion.Admin.Data.Models/Agents/WorkflowOptions.cs` — `Id`
- `src/libs/Legion.Admin.Data.Models/Providers/ProviderOptions.cs` — `Id`
- `src/libs/Legion.Admin.Data.Models/SecretOptions.cs` — `Id`

**Modified (configuration files — add value converters):**
- `src/libs/Legion.Admin.Data/Configurations/AgentOptionsConfiguration.cs`
- `src/libs/Legion.Admin.Data/Configurations/AgentTemplatesConfiguration.cs`
- `src/libs/Legion.Admin.Data/Configurations/McpServerOptionsConfiguration.cs`
- `src/libs/Legion.Admin.Data/Configurations/ProviderOptionsConfiguration.cs`
- `src/libs/Legion.Admin.Data/Configurations/SecretOptionsConfiguration.cs`

**Created (new configurations for entities lacking explicit ones):**
- `src/libs/Legion.Admin.Data/Configurations/MemoryOptionsConfiguration.cs`
- `src/libs/Legion.Admin.Data/Configurations/MiddlewareOptionsConfiguration.cs`
- `src/libs/Legion.Admin.Data/Configurations/ModelOptionsConfiguration.cs`
- `src/libs/Legion.Admin.Data/Configurations/SkillOptionsConfiguration.cs`
- `src/libs/Legion.Admin.Data/Configurations/ToolOptionsConfiguration.cs`
- `src/libs/Legion.Admin.Data/Configurations/WorkflowOptionsConfiguration.cs`
- `src/libs/Legion.Admin.Data/Configurations/McpServerHeadersConfiguration.cs`

**Modified (stores and interface):**
- `src/libs/Legion.Admin.Data/Stores/IStore.cs`
- `src/libs/Legion.Admin.Data/Stores/CatalogStore.cs`
- `src/libs/Legion.Admin.Data/Stores/AgentStore.cs`
- `src/libs/Legion.Admin.Data/Stores/McpStore.cs`
- `src/libs/Legion.Admin.Data/Stores/MemoryStore.cs`
- `src/libs/Legion.Admin.Data/Stores/ProviderStore.cs`
- `src/libs/Legion.Admin.Data/Stores/SecretsStore.cs` (interface `ISecretsStore`)
- `src/libs/Legion.Admin.Data.Sqlite/Stores/SqliteSecretsStore.cs`
- `src/libs/Legion.Admin.Data.PostgreSQL/Stores/PostgreSqlSecretsStore.cs`

**Modified (services and controllers):**
- `src/libs/Legion.Admin.Data/Services/SecretsManager.cs`
- `src/WebDev/Controllers/SecretsController.cs`

**Modified (Razor pages — `[Parameter] public int Id` → `Guid`):**
- `src/libs/Legion.Admin.UI/_Imports.razor` — add base models namespace for branded ID types
- `src/libs/Legion.Admin.UI/Pages/Agents/Agents.Create.razor`
- `src/libs/Legion.Admin.UI/Pages/Agents/Agents.Update.razor`
- `src/libs/Legion.Admin.UI/Pages/Agents/Agents.Delete.razor`
- `src/libs/Legion.Admin.UI/Pages/Mcps/Mcps.Create.razor`
- `src/libs/Legion.Admin.UI/Pages/Mcps/Mcps.Update.razor`
- `src/libs/Legion.Admin.UI/Pages/Mcps/Mcps.Delete.razor`
- `src/libs/Legion.Admin.UI/Pages/Memory/Memory.Update.razor`
- `src/libs/Legion.Admin.UI/Pages/Memory/Memory.Delete.razor`
- `src/libs/Legion.Admin.UI/Pages/Middleware/Middleware.Create.razor`
- `src/libs/Legion.Admin.UI/Pages/Middleware/Middleware.Update.razor`
- `src/libs/Legion.Admin.UI/Pages/Middleware/Middleware.Delete.razor`
- `src/libs/Legion.Admin.UI/Pages/Models/Models.Create.razor`
- `src/libs/Legion.Admin.UI/Pages/Models/Models.Update.razor`
- `src/libs/Legion.Admin.UI/Pages/Models/Models.Delete.razor`
- `src/libs/Legion.Admin.UI/Pages/Providers/Providers.Create.razor`
- `src/libs/Legion.Admin.UI/Pages/Providers/Providers.Update.razor`
- `src/libs/Legion.Admin.UI/Pages/Providers/Providers.Delete.razor`
- `src/libs/Legion.Admin.UI/Pages/Skills/Skills.Create.razor`
- `src/libs/Legion.Admin.UI/Pages/Skills/Skills.Update.razor`
- `src/libs/Legion.Admin.UI/Pages/Skills/Skills.Delete.razor`
- `src/libs/Legion.Admin.UI/Pages/Tools/Tools.Create.razor`
- `src/libs/Legion.Admin.UI/Pages/Tools/Tools.Update.razor`
- `src/libs/Legion.Admin.UI/Pages/Tools/Tools.Delete.razor`
- `src/libs/Legion.Admin.UI/Pages/Workflows/Workflows.Create.razor`
- `src/libs/Legion.Admin.UI/Pages/Workflows/Workflows.Update.razor`
- `src/libs/Legion.Admin.UI/Pages/Workflows/Workflows.Delete.razor`
- `src/WebDev/Components/Pages/Secrets.razor` (uses `.Id` from `SecretOptions`)

**Modified (Razor components):**
- `src/libs/Legion.Admin.UI/Components/MemoryEditDialog.razor` — `AgentId`, `MemoryOptions.Id`, `MemoryOptions.AgentId`

**Modified (seed and tests):**
- `src/libs/Legion.Admin.Data/Seeds/SeedData.Agents.cs`
- `tests/Legion.Secrets.Tests/SqliteSecretsStoreTests.cs`
- `tests/Legion.Secrets.Tests/SecretsManagerTests.cs`
- `tests/Legion.Secrets.Tests/SecretResolvingHandlerTests.cs`
- `tests/Legion.Secrets.Tests/SecretsControllerTests.cs`

**Deleted (App migrations only — Auth migrations untouched):**
- `src/libs/Legion.Admin.Data.PostgreSQL/Migrations/App/*.cs`
- `src/libs/Legion.Admin.Data.Sqlite/Migrations/App/*.cs`

**Regenerated:**
- Fresh `InitialCreate` migration in both PostgreSQL and SQLite App migration folders

---

## Task 1: Confirm baseline build and test pass

**Files:**
- None (orientation only)

- [ ] **Step 1: Verify the solution builds before any changes**

Run: `dotnet build /home/timm/Legion/Legion.sln`
Expected: Build succeeds with no errors. Note any pre-existing warnings.

- [ ] **Step 2: Verify existing tests pass**

Run: `dotnet test /home/timm/Legion/tests/Legion.Secrets.Tests/Legion.Secrets.Tests.csproj`
Expected: All tests pass. Record the test count for later comparison.

- [ ] **Step 3: Confirm working tree is clean and on master**

Run: `git status && git branch --show-current`
Expected: `nothing to commit, working tree clean` and branch `master`.

---

## Task 2: Create the branded ID types

**Files:**
- Create: `src/libs/Legion.Admin.Data.Models/Ids.cs`

- [ ] **Step 1: Write the file with all 11 branded ID types**

Create `src/libs/Legion.Admin.Data.Models/Ids.cs` with this content:

```csharp
namespace Legion.Admin.Data.Models;

public readonly record struct AgentOptionsId(Guid Value)
{
    public static AgentOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(AgentOptionsId id) => id.Value;
    public static implicit operator AgentOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct McpServerOptionsId(Guid Value)
{
    public static McpServerOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(McpServerOptionsId id) => id.Value;
    public static implicit operator McpServerOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct McpServerHeadersId(Guid Value)
{
    public static McpServerHeadersId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(McpServerHeadersId id) => id.Value;
    public static implicit operator McpServerHeadersId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct MemoryOptionsId(Guid Value)
{
    public static MemoryOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(MemoryOptionsId id) => id.Value;
    public static implicit operator MemoryOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct MiddlewareOptionsId(Guid Value)
{
    public static MiddlewareOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(MiddlewareOptionsId id) => id.Value;
    public static implicit operator MiddlewareOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ModelOptionsId(Guid Value)
{
    public static ModelOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(ModelOptionsId id) => id.Value;
    public static implicit operator ModelOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ProviderOptionsId(Guid Value)
{
    public static ProviderOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(ProviderOptionsId id) => id.Value;
    public static implicit operator ProviderOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct SkillOptionsId(Guid Value)
{
    public static SkillOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(SkillOptionsId id) => id.Value;
    public static implicit operator SkillOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct ToolOptionsId(Guid Value)
{
    public static ToolOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(ToolOptionsId id) => id.Value;
    public static implicit operator ToolOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct WorkflowOptionsId(Guid Value)
{
    public static WorkflowOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(WorkflowOptionsId id) => id.Value;
    public static implicit operator WorkflowOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}

public readonly record struct SecretOptionsId(Guid Value)
{
    public static SecretOptionsId New() => new(Guid.CreateVersion7());
    public static implicit operator Guid(SecretOptionsId id) => id.Value;
    public static implicit operator SecretOptionsId(Guid value) => new(value);
    public override string ToString() => Value.ToString();
}
```

Note: `AgentTemplate` inherits from `AgentOptions` and reuses `AgentOptionsId` — no separate type needed.

- [ ] **Step 2: Build only the Models project to confirm syntax**

Run: `dotnet build /home/timm/Legion/src/libs/Legion.Admin.Data.Models/Legion.Admin.Data.Models.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
cd /home/timm/Legion
git add src/libs/Legion.Admin.Data.Models/Ids.cs
git commit -m "feat: add 11 branded UUID v7 ID record structs

Each entity ID is a readonly record struct wrapping Guid with implicit
conversions, .New() factory using Guid.CreateVersion7(), and ToString
override. Auth tables excluded (already use string GUIDs)."
```

---

## Task 3: Update SecretOptions model

**Files:**
- Modify: `src/libs/Legion.Admin.Data.Models/SecretOptions.cs`

- [ ] **Step 1: Replace `int Id` with `SecretOptionsId Id`**

Replace the entire file with:

```csharp
namespace Legion.Admin.Data.Models;

public record SecretOptions
{
    public SecretOptionsId Id { get; init; }
    public string Path { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EncryptedValue { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 2: Build the Models project**

Run: `dotnet build /home/timm/Legion/src/libs/Legion.Admin.Data.Models/Legion.Admin.Data.Models.csproj`
Expected: Build succeeds.

- [ ] **Step 3: Commit**

```bash
cd /home/timm/Legion
git add src/libs/Legion.Admin.Data.Models/SecretOptions.cs
git commit -m "refactor: SecretOptions.Id uses branded SecretOptionsId"
```

---

## Task 4: Update Provider/Model/Skill/Tool/Workflow/Middleware models (no FKs)

These six models only have an `Id` property — no foreign key columns to convert.

**Files:**
- Modify: `src/libs/Legion.Admin.Data.Models/Providers/ProviderOptions.cs`
- Modify: `src/libs/Legion.Admin.Data.Models/Agents/ModelOptions.cs`
- Modify: `src/libs/Legion.Admin.Data.Models/Agents/SkillOptions.cs`
- Modify: `src/libs/Legion.Admin.Data.Models/Agents/ToolOptions.cs`
- Modify: `src/libs/Legion.Admin.Data.Models/Agents/WorkflowOptions.cs`
- Modify: `src/libs/Legion.Admin.Data.Models/Agents/MiddlewareOptions.cs`

- [ ] **Step 1: Update `ProviderOptions.cs`**

Replace `public int Id { get; init; }` with `public ProviderOptionsId Id { get; init; }` (line 21). Keep all other content unchanged.

- [ ] **Step 2: Update `ModelOptions.cs`**

Replace `public int Id { get; set; }` with `public ModelOptionsId Id { get; set; }` (line 7). Keep all other content unchanged.

- [ ] **Step 3: Update `SkillOptions.cs`**

Replace `public int Id { get; init; }` with `public SkillOptionsId Id { get; init; }` (line 5). Keep all other content unchanged.

- [ ] **Step 4: Update `ToolOptions.cs`**

Replace `public int Id { get; init; }` with `public ToolOptionsId Id { get; init; }` (line 5). Keep all other content unchanged.

- [ ] **Step 5: Update `WorkflowOptions.cs`**

Replace `public int Id { get; init; }` with `public WorkflowOptionsId Id { get; init; }` (line 5). Keep all other content unchanged.

- [ ] **Step 6: Update `MiddlewareOptions.cs`**

Replace `public int Id { get; set; }` with `public MiddlewareOptionsId Id { get; set; }` (line 22). Keep all other content unchanged.

- [ ] **Step 7: Build the Models project**

Run: `dotnet build /home/timm/Legion/src/libs/Legion.Admin.Data.Models/Legion.Admin.Data.Models.csproj`
Expected: Build succeeds.

- [ ] **Step 8: Commit**

```bash
cd /home/timm/Legion
git add src/libs/Legion.Admin.Data.Models/Providers/ProviderOptions.cs \
        src/libs/Legion.Admin.Data.Models/Agents/ModelOptions.cs \
        src/libs/Legion.Admin.Data.Models/Agents/SkillOptions.cs \
        src/libs/Legion.Admin.Data.Models/Agents/ToolOptions.cs \
        src/libs/Legion.Admin.Data.Models/Agents/WorkflowOptions.cs \
        src/libs/Legion.Admin.Data.Models/Agents/MiddlewareOptions.cs
git commit -m "refactor: 6 catalog entities use branded ID types

ProviderOptions, ModelOptions, SkillOptions, ToolOptions, WorkflowOptions,
MiddlewareOptions all switch from int Id to their branded counterparts."
```

---

## Task 5: Update AgentOptions, MemoryOptions, McpServerOptions (have FKs)

**Files:**
- Modify: `src/libs/Legion.Admin.Data.Models/Agents/AgentOptions.cs`
- Modify: `src/libs/Legion.Admin.Data.Models/Agents/MemoryOptions.cs`
- Modify: `src/libs/Legion.Admin.Data.Models/Agents/McpServerOptions.cs`

- [ ] **Step 1: Update `AgentOptions.cs`**

Replace the file content with:

```csharp
using System.Runtime.InteropServices;
using Legion.Admin.Data.Models.Providers;

namespace Legion.Admin.Data.Models.Agents;

public record AgentOptions
{
    public AgentOptionsId Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Instructions { get; init; }
    public int? MaxTokens { get; init; }
    public ProviderOptionsId ProviderId { get; set; }
    public ProviderOptions? Provider { get; set; }
    // public MemoryOptionsId MemoryId { get; set; }
    public MemoryOptions? Memory { get; set; }
    public List<ModelOptions> Models { get; set; } = [];
    public List<SkillOptions> Skills { get; set; } = [];
    public List<ToolOptions> Tools { get; set; } = [];
    public List<McpServerOptions> McpServers { get; set; } = [];
    public List<MiddlewareOptions> Middleware { get; set; } = [];
}
```

- [ ] **Step 2: Update `MemoryOptions.cs`**

Replace the file content with:

```csharp
namespace Legion.Admin.Data.Models.Agents;

public record MemoryOptions
{
    public MemoryOptionsId Id { get; init; }

    public AgentOptionsId AgentId { get; set; }
    public AgentOptions? Agent { get; set; }

    public SearchBehavior SearchTime { get; init; } = SearchBehavior.BeforeAIInvoke;
    public int? MaxResults { get; init; }
    public string? FunctionToolName { get; init; }
    public string? FunctionToolDescription { get; init; }
    public string? ContextPrompt { get; init; }
    public string? StateKey { get; init; }
}
```

- [ ] **Step 3: Update `McpServerOptions.cs`**

Replace the file content with:

```csharp
namespace Legion.Admin.Data.Models.Agents;

public enum TransportType
{
    StdinOut,
    Http,
    WebSocket,
    Grpc
}

public record McpServerHeaders
{
    public McpServerHeadersId Id { get; set; }
    public string? Key { get; set; }
    public string? Value { get; set; }
    public McpServerOptionsId McpServerId { get; set; }
    public McpServerOptions? McpServer { get; set; }
}

public record McpServerOptions
{
    public McpServerOptionsId Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? ServerUrl { get; set; }
    public string? ServerLabel { get; set; }
    public TransportType Transport { get; set; }
    public bool RequireApproval { get; set; }
    public string? CommandLine { get; set; }
    public List<AgentOptions> Agents { get; set; } = [];
    public List<McpServerHeaders> Headers { get; set; } = [];
}
```

- [ ] **Step 4: Build the Models project**

Run: `dotnet build /home/timm/Legion/src/libs/Legion.Admin.Data.Models/Legion.Admin.Data.Models.csproj`
Expected: Build succeeds.

- [ ] **Step 5: Commit**

```bash
cd /home/timm/Legion
git add src/libs/Legion.Admin.Data.Models/Agents/AgentOptions.cs \
        src/libs/Legion.Admin.Data.Models/Agents/MemoryOptions.cs \
        src/libs/Legion.Admin.Data.Models/Agents/McpServerOptions.cs
git commit -m "refactor: AgentOptions/MemoryOptions/McpServerOptions branded IDs and FKs

ProviderId, AgentId, McpServerId now strongly typed."
```

---

## Task 6: Update existing EF Core configurations to register value converters

**Files:**
- Modify: `src/libs/Legion.Admin.Data/Configurations/SecretOptionsConfiguration.cs`
- Modify: `src/libs/Legion.Admin.Data/Configurations/AgentOptionsConfiguration.cs`
- Modify: `src/libs/Legion.Admin.Data/Configurations/AgentTemplatesConfiguration.cs`
- Modify: `src/libs/Legion.Admin.Data/Configurations/McpServerOptionsConfiguration.cs`
- Modify: `src/libs/Legion.Admin.Data/Configurations/ProviderOptionsConfiguration.cs`

- [ ] **Step 1: Update `SecretOptionsConfiguration.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class SecretOptionsConfiguration : IEntityTypeConfiguration<SecretOptions>
{
    public void Configure(EntityTypeBuilder<SecretOptions> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new SecretOptionsId(value));
        builder.Property(s => s.Path).IsRequired().HasMaxLength(500);
        builder.Property(s => s.EncryptedValue).IsRequired();
        builder.HasIndex(s => s.Path).IsUnique();
    }
}
```

- [ ] **Step 2: Update `AgentOptionsConfiguration.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class AgentOptionsConfiguration : IEntityTypeConfiguration<AgentOptions>
{
    private const string Schema = "agents";

    public void Configure(EntityTypeBuilder<AgentOptions> builder)
    {
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new AgentOptionsId(value));
        builder.Property(a => a.ProviderId)
            .HasConversion(id => id.Value, value => new ProviderOptionsId(value));

        builder.HasMany(a => a.McpServers)
            .WithMany(m => m.Agents)
            .UsingEntity(t => t.ToTable("AgentMcpServers", schema: Schema));

        builder.HasOne(a => a.Memory)
            .WithOne(m => m.Agent)
            .HasForeignKey<MemoryOptions>(m => m.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Middleware)
            .WithMany(m => m.Agents)
            .UsingEntity(t => t.ToTable("AgentMiddleware", schema: Schema));

        builder.HasMany(a => a.Models)
            .WithMany(m => m.Agents)
            .UsingEntity(t => t.ToTable("AgentModels", schema: Schema));

        builder.HasOne(a => a.Provider)
            .WithMany(p => p.Agents)
            .HasForeignKey(a => a.ProviderId);

        builder.HasMany(a => a.Tools)
            .WithMany(t => t.Agents)
            .UsingEntity(t => t.ToTable("AgentTools", schema: Schema));

        builder.HasMany(a => a.Skills)
            .WithMany(s => s.Agents)
            .UsingEntity(t => t.ToTable("AgentSkills", schema: Schema));
    }
}
```

- [ ] **Step 3: Update `AgentTemplatesConfiguration.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Legion.Admin.Data.Configurations;

public class AgentTemplatesConfiguration : IEntityTypeConfiguration<AgentTemplate>
{
    public void Configure(EntityTypeBuilder<AgentTemplate> builder)
    {
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new AgentOptionsId(value));
        builder.Property(a => a.ProviderId)
            .HasConversion(id => id.Value, value => new ProviderOptionsId(value));

        builder.HasMany(a => a.McpServers);
        builder.HasOne(a => a.Memory);
        builder.HasMany(a => a.Middleware);
        builder.HasMany(a => a.Models);
        builder.HasOne(a => a.Provider);
        builder.HasMany(a => a.Tools);
        builder.HasMany(a => a.Skills);
    }
}
```

- [ ] **Step 4: Update `McpServerOptionsConfiguration.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class McpServerOptionsConfiguration : IEntityTypeConfiguration<McpServerOptions>
{
    private const string Schema = "agents";
    public void Configure(EntityTypeBuilder<McpServerOptions> builder)
    {
        builder.ToTable("McpServers", schema: Schema);
        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => new McpServerOptionsId(value));
        builder.HasMany(m => m.Headers)
            .WithOne(h => h.McpServer)
            .HasForeignKey(h => h.McpServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 5: Update `ProviderOptionsConfiguration.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class ProviderOptionsConfiguration : IEntityTypeConfiguration<ProviderOptions>
{
    private const string Schema = "agents";

    public void Configure(EntityTypeBuilder<ProviderOptions> builder)
    {
        builder.ToTable("Providers", schema: Schema);
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new ProviderOptionsId(value));
        builder.HasMany(p => p.Models)
            .WithMany(m => m.Providers)
            .UsingEntity(t => t.ToTable("ProviderModels", schema: Schema));
    }
}
```

- [ ] **Step 6: Build the Legion.Admin.Data project**

Run: `dotnet build /home/timm/Legion/src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj`
Expected: Build succeeds.

- [ ] **Step 7: Commit**

```bash
cd /home/timm/Legion
git add src/libs/Legion.Admin.Data/Configurations/SecretOptionsConfiguration.cs \
        src/libs/Legion.Admin.Data/Configurations/AgentOptionsConfiguration.cs \
        src/libs/Legion.Admin.Data/Configurations/AgentTemplatesConfiguration.cs \
        src/libs/Legion.Admin.Data/Configurations/McpServerOptionsConfiguration.cs \
        src/libs/Legion.Admin.Data/Configurations/ProviderOptionsConfiguration.cs
git commit -m "refactor: register branded ID converters in existing EF configurations"
```

---

## Task 7: Create new EF Core configurations for entities lacking explicit ones

EF Core automatically discovers entities through `DbSet<T>` even without an explicit `IEntityTypeConfiguration<T>`, but we need explicit configs to register the value converters for `Memory`, `Middleware`, `Model`, `Skill`, `Tool`, `Workflow`, and `McpServerHeaders`.

**Files:**
- Create: `src/libs/Legion.Admin.Data/Configurations/MemoryOptionsConfiguration.cs`
- Create: `src/libs/Legion.Admin.Data/Configurations/MiddlewareOptionsConfiguration.cs`
- Create: `src/libs/Legion.Admin.Data/Configurations/ModelOptionsConfiguration.cs`
- Create: `src/libs/Legion.Admin.Data/Configurations/SkillOptionsConfiguration.cs`
- Create: `src/libs/Legion.Admin.Data/Configurations/ToolOptionsConfiguration.cs`
- Create: `src/libs/Legion.Admin.Data/Configurations/WorkflowOptionsConfiguration.cs`
- Create: `src/libs/Legion.Admin.Data/Configurations/McpServerHeadersConfiguration.cs`

- [ ] **Step 1: Create `MemoryOptionsConfiguration.cs`**

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class MemoryOptionsConfiguration : IEntityTypeConfiguration<MemoryOptions>
{
    public void Configure(EntityTypeBuilder<MemoryOptions> builder)
    {
        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => new MemoryOptionsId(value));
        builder.Property(m => m.AgentId)
            .HasConversion(id => id.Value, value => new AgentOptionsId(value));
    }
}
```

- [ ] **Step 2: Create `MiddlewareOptionsConfiguration.cs`**

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class MiddlewareOptionsConfiguration : IEntityTypeConfiguration<MiddlewareOptions>
{
    public void Configure(EntityTypeBuilder<MiddlewareOptions> builder)
    {
        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => new MiddlewareOptionsId(value));
    }
}
```

- [ ] **Step 3: Create `ModelOptionsConfiguration.cs`**

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class ModelOptionsConfiguration : IEntityTypeConfiguration<ModelOptions>
{
    public void Configure(EntityTypeBuilder<ModelOptions> builder)
    {
        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => new ModelOptionsId(value));
    }
}
```

- [ ] **Step 4: Create `SkillOptionsConfiguration.cs`**

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class SkillOptionsConfiguration : IEntityTypeConfiguration<SkillOptions>
{
    public void Configure(EntityTypeBuilder<SkillOptions> builder)
    {
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new SkillOptionsId(value));
    }
}
```

- [ ] **Step 5: Create `ToolOptionsConfiguration.cs`**

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class ToolOptionsConfiguration : IEntityTypeConfiguration<ToolOptions>
{
    public void Configure(EntityTypeBuilder<ToolOptions> builder)
    {
        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => new ToolOptionsId(value));
    }
}
```

- [ ] **Step 6: Create `WorkflowOptionsConfiguration.cs`**

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class WorkflowOptionsConfiguration : IEntityTypeConfiguration<WorkflowOptions>
{
    public void Configure(EntityTypeBuilder<WorkflowOptions> builder)
    {
        builder.Property(w => w.Id)
            .HasConversion(id => id.Value, value => new WorkflowOptionsId(value));
    }
}
```

- [ ] **Step 7: Create `McpServerHeadersConfiguration.cs`**

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class McpServerHeadersConfiguration : IEntityTypeConfiguration<McpServerHeaders>
{
    public void Configure(EntityTypeBuilder<McpServerHeaders> builder)
    {
        builder.Property(h => h.Id)
            .HasConversion(id => id.Value, value => new McpServerHeadersId(value));
        builder.Property(h => h.McpServerId)
            .HasConversion(id => id.Value, value => new McpServerOptionsId(value));
    }
}
```

- [ ] **Step 8: Build the Legion.Admin.Data project**

Run: `dotnet build /home/timm/Legion/src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj`
Expected: Build fails — store and seed code still reference `int` IDs. This is expected; we fix it next.

- [ ] **Step 9: Commit**

```bash
cd /home/timm/Legion
git add src/libs/Legion.Admin.Data/Configurations/MemoryOptionsConfiguration.cs \
        src/libs/Legion.Admin.Data/Configurations/MiddlewareOptionsConfiguration.cs \
        src/libs/Legion.Admin.Data/Configurations/ModelOptionsConfiguration.cs \
        src/libs/Legion.Admin.Data/Configurations/SkillOptionsConfiguration.cs \
        src/libs/Legion.Admin.Data/Configurations/ToolOptionsConfiguration.cs \
        src/libs/Legion.Admin.Data/Configurations/WorkflowOptionsConfiguration.cs \
        src/libs/Legion.Admin.Data/Configurations/McpServerHeadersConfiguration.cs
git commit -m "feat: add EF configurations registering branded ID converters

Memory, Middleware, Model, Skill, Tool, Workflow, McpServerHeaders entities
now have explicit IEntityTypeConfiguration registering UUID v7 converters."
```

---

## Task 8: Update IStore<T> interface and CatalogStore base class

**Files:**
- Modify: `src/libs/Legion.Admin.Data/Stores/IStore.cs`
- Modify: `src/libs/Legion.Admin.Data/Stores/CatalogStore.cs`

The `IStore<T>` interface uses `int id` and is generic over the entity type. To support branded IDs we widen the interface to take `Guid id` (since every branded type implicitly converts to `Guid`).

- [ ] **Step 1: Update `IStore.cs`**

Replace the file content with:

```csharp
namespace Legion.Admin.Data.Stores;

public interface IStore<TEntity> where TEntity : class
{
    string AllKey { get; }
    Task<List<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task<TEntity?> GetAsync(Guid id, CancellationToken ct = default);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 2: Update `CatalogStore.cs`**

Replace the file content with:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Legion.Admin.Data.Stores;

public abstract class CatalogStore<TEntity>(
    AppDbContext db,
    IMemoryCache cache,
    Func<Guid, object> keyFactory)
    : IStore<TEntity>
    where TEntity : class
{
    protected AppDbContext Db { get; } = db;
    public abstract string AllKey { get; }

    private static readonly MemoryCacheEntryOptions CacheOptions =
        new() { SlidingExpiration = TimeSpan.FromMinutes(5) };

    protected void InvalidateCache() => cache.Remove(AllKey);

    public async Task<List<TEntity>> GetAllAsync(CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync(AllKey, async e =>
        {
            e.SetOptions(CacheOptions);
            return await BuildAllQuery().ToListAsync(ct);
        }))!;

    protected virtual IQueryable<TEntity> BuildAllQuery() =>
        Db.Set<TEntity>().AsNoTracking();

    public virtual async Task<TEntity?> GetAsync(Guid id, CancellationToken ct = default) =>
        await Db.Set<TEntity>().FindAsync([keyFactory(id)], ct);

    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
    {
        Db.Set<TEntity>().Add(entity);
        await Db.SaveChangesAsync(ct);
        cache.Remove(AllKey);
        return entity;
    }

    public async Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        Db.ChangeTracker.Clear();
        Db.Set<TEntity>().Update(entity);
        await Db.SaveChangesAsync(ct);
        cache.Remove(AllKey);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await Db.Set<TEntity>().FindAsync([keyFactory(id)], ct);
        if (entity is not null)
        {
            Db.Set<TEntity>().Remove(entity);
            await Db.SaveChangesAsync(ct);
            cache.Remove(AllKey);
        }
    }
}
```

- [ ] **Step 3: Commit (build will still fail — concrete stores fixed next)**

```bash
cd /home/timm/Legion
git add src/libs/Legion.Admin.Data/Stores/IStore.cs \
        src/libs/Legion.Admin.Data/Stores/CatalogStore.cs
git commit -m "refactor: IStore<T> and CatalogStore use Guid id

Branded IDs implicitly convert to Guid for callers, while CatalogStore
converts Guid back to the entity key type before FindAsync/DeleteAsync."
```

---

## Task 9: Update concrete catalog stores (McpStore, ProviderStore, ToolStore, etc.)

Every concrete catalog store must pass a `Guid` -> branded ID key factory into `CatalogStore<T>`. This is required because `FindAsync` and `DeleteAsync` need the actual CLR key type, not a raw `Guid`, once the entity key properties are branded record structs.

**Files:**
- Modify: `src/libs/Legion.Admin.Data/Stores/McpStore.cs`
- Modify: `src/libs/Legion.Admin.Data/Stores/ProviderStore.cs`
- Modify: `src/libs/Legion.Admin.Data/Stores/ToolStore.cs`
- Modify: `src/libs/Legion.Admin.Data/Stores/SkillStore.cs`
- Modify: `src/libs/Legion.Admin.Data/Stores/ModelStore.cs`
- Modify: `src/libs/Legion.Admin.Data/Stores/WorkflowStore.cs`
- Modify: `src/libs/Legion.Admin.Data/Stores/MiddlewareStore.cs`

- [ ] **Step 1: Update `McpStore.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Legion.Admin.Data.Stores;

public class McpStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<McpServerOptions>(db, cache, id => (McpServerOptionsId)id)
{
    public override string AllKey => "Mcps:all";

    protected override IQueryable<McpServerOptions> BuildAllQuery() =>
        Db.Mcps.AsNoTracking().Include(m => m.Headers);

    public override async Task<McpServerOptions?> GetAsync(Guid id, CancellationToken ct = default) =>
        await Db.Mcps.AsNoTracking().Include(m => m.Headers)
            .FirstOrDefaultAsync(m => m.Id == (McpServerOptionsId)id, ct);

    public async Task ReplaceHeadersAsync(Guid mcpId, List<McpServerHeaders> headers, CancellationToken ct = default)
    {
        var typedMcpId = (McpServerOptionsId)mcpId;
        var existing = await Db.McpServerHeaders.Where(h => h.McpServerId == typedMcpId).ToListAsync(ct);
        Db.McpServerHeaders.RemoveRange(existing);
        foreach (var h in headers)
        {
            if (h.Id.Value == Guid.Empty)
                h.Id = McpServerHeadersId.New();
            h.McpServerId = typedMcpId;
        }
        Db.McpServerHeaders.AddRange(headers);
        await Db.SaveChangesAsync(ct);
        InvalidateCache();
    }
}
```

- [ ] **Step 2: Update `ProviderStore.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Legion.Admin.Data.Stores;

public class ProviderStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<ProviderOptions>(db, cache, id => (ProviderOptionsId)id)
{
    public override string AllKey => "Providers:all";

    protected override IQueryable<ProviderOptions> BuildAllQuery() =>
        Db.Providers.AsNoTracking().Include(p => p.Models);

    public override async Task<ProviderOptions?> GetAsync(Guid id, CancellationToken ct = default) =>
        await Db.Providers.AsNoTracking().Include(p => p.Models)
            .FirstOrDefaultAsync(p => p.Id == (ProviderOptionsId)id, ct);

    public async Task AssignModelsAsync(Guid providerId, IEnumerable<Guid> modelIds, CancellationToken ct = default)
    {
        var typedProviderId = (ProviderOptionsId)providerId;
        var provider = await Db.Providers.Include(p => p.Models)
            .FirstOrDefaultAsync(p => p.Id == typedProviderId, ct);
        if (provider is null) return;
        var typedIds = modelIds.Select(g => (ModelOptionsId)g).ToList();
        var models = await Db.Models.Where(m => typedIds.Contains(m.Id)).ToListAsync(ct);
        provider.Models.Clear();
        provider.Models.AddRange(models);
        await Db.SaveChangesAsync(ct);
        InvalidateCache();
    }
}
```

- [ ] **Step 3: Update `ToolStore.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.Extensions.Caching.Memory;

namespace Legion.Admin.Data.Stores;

public class ToolStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<ToolOptions>(db, cache, id => (ToolOptionsId)id)
{
    public override string AllKey => "Tools:all";
}
```

- [ ] **Step 4: Update `SkillStore.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.Extensions.Caching.Memory;

namespace Legion.Admin.Data.Stores;

public class SkillStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<SkillOptions>(db, cache, id => (SkillOptionsId)id)
{
    public override string AllKey => "Skills:all";
}
```

- [ ] **Step 5: Update `ModelStore.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Providers;
using Microsoft.Extensions.Caching.Memory;

namespace Legion.Admin.Data.Stores;

public class ModelStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<ModelOptions>(db, cache, id => (ModelOptionsId)id)
{
    public override string AllKey => "Models:all";
}
```

- [ ] **Step 6: Update `WorkflowStore.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.Extensions.Caching.Memory;

namespace Legion.Admin.Data.Stores;

public class WorkflowStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<WorkflowOptions>(db, cache, id => (WorkflowOptionsId)id)
{
    public override string AllKey => "Workflows:all";
}
```

- [ ] **Step 7: Update `MiddlewareStore.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.Extensions.Caching.Memory;

namespace Legion.Admin.Data.Stores;

public class MiddlewareStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<MiddlewareOptions>(db, cache, id => (MiddlewareOptionsId)id)
{
    public override string AllKey => "Middleware:all";
}
```

- [ ] **Step 8: Commit**

```bash
cd /home/timm/Legion
git add src/libs/Legion.Admin.Data/Stores/McpStore.cs \
        src/libs/Legion.Admin.Data/Stores/ProviderStore.cs \
        src/libs/Legion.Admin.Data/Stores/ToolStore.cs \
        src/libs/Legion.Admin.Data/Stores/SkillStore.cs \
        src/libs/Legion.Admin.Data/Stores/ModelStore.cs \
        src/libs/Legion.Admin.Data/Stores/WorkflowStore.cs \
        src/libs/Legion.Admin.Data/Stores/MiddlewareStore.cs
git commit -m "refactor: catalog stores convert Guid IDs to branded keys

CatalogStore receives a key factory per entity so inherited GetAsync and
DeleteAsync call FindAsync with the actual branded key CLR type. McpStore
and ProviderStore also convert assignment parameters to branded IDs for FK
comparisons."
```

---

## Task 10: Update AgentStore and MemoryStore

**Files:**
- Modify: `src/libs/Legion.Admin.Data/Stores/AgentStore.cs`
- Modify: `src/libs/Legion.Admin.Data/Stores/MemoryStore.cs`

- [ ] **Step 1: Update `AgentStore.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Legion.Admin.Data.Models.Providers;
using Microsoft.EntityFrameworkCore;

namespace Legion.Admin.Data.Stores;

public class AgentStore(AppDbContext db)
{
    public async Task<List<AgentOptions>> GetAllAsync(CancellationToken ct = default) =>
        await db.Agents.AsNoTracking()
            .Include(a => a.Provider)
            .Include(a => a.Tools)
            .Include(a => a.Models)
            .Include(a => a.Skills)
            .Include(a => a.McpServers)
            .Include(a => a.Middleware)
            .ToListAsync(ct);

    public async Task<AgentOptions?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var typedId = (AgentOptionsId)id;
        return await db.Agents.AsNoTracking()
            .Include(a => a.Provider)
            .Include(a => a.Tools)
            .Include(a => a.Models)
            .Include(a => a.Skills)
            .Include(a => a.McpServers)
            .Include(a => a.Middleware)
            .Include(a => a.Memory)
            .FirstOrDefaultAsync(a => a.Id == typedId, ct);
    }

    public async Task<AgentOptions> AddAsync(AgentOptions agent, CancellationToken ct = default)
    {
        db.Agents.Add(agent);
        await db.SaveChangesAsync(ct);
        return agent;
    }

    public async Task UpdateAsync(AgentOptions agent, CancellationToken ct = default)
    {
        db.ChangeTracker.Clear();
        db.Agents.Update(agent);
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignToolsAsync(Guid agentId, IEnumerable<Guid> toolIds, CancellationToken ct = default)
    {
        var typedAgentId = (AgentOptionsId)agentId;
        var agent = await db.Agents.Include(a => a.Tools).FirstOrDefaultAsync(a => a.Id == typedAgentId, ct);
        if (agent is null) return;
        var typedToolIds = toolIds.Select(g => (ToolOptionsId)g).ToList();
        var tools = await db.Tools.Where(t => typedToolIds.Contains(t.Id)).ToListAsync(ct);
        agent.Tools.Clear();
        agent.Tools.AddRange(tools);
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignModelsAsync(Guid agentId, IEnumerable<Guid> modelIds, CancellationToken ct = default)
    {
        var typedAgentId = (AgentOptionsId)agentId;
        var agent = await db.Agents.Include(a => a.Models).FirstOrDefaultAsync(a => a.Id == typedAgentId, ct);
        if (agent is null) return;
        var typedModelIds = modelIds.Select(g => (ModelOptionsId)g).ToList();
        var models = await db.Models.Where(m => typedModelIds.Contains(m.Id)).ToListAsync(ct);
        agent.Models.Clear();
        agent.Models.AddRange(models);
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignSkillsAsync(Guid agentId, IEnumerable<Guid> skillIds, CancellationToken ct = default)
    {
        var typedAgentId = (AgentOptionsId)agentId;
        var agent = await db.Agents.Include(a => a.Skills).FirstOrDefaultAsync(a => a.Id == typedAgentId, ct);
        if (agent is null) return;
        var typedSkillIds = skillIds.Select(g => (SkillOptionsId)g).ToList();
        var skills = await db.Skills.Where(s => typedSkillIds.Contains(s.Id)).ToListAsync(ct);
        agent.Skills.Clear();
        agent.Skills.AddRange(skills);
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignMcpServersAsync(Guid agentId, IEnumerable<Guid> mcpIds, CancellationToken ct = default)
    {
        var typedAgentId = (AgentOptionsId)agentId;
        var agent = await db.Agents.Include(a => a.McpServers).FirstOrDefaultAsync(a => a.Id == typedAgentId, ct);
        if (agent is null) return;
        var typedMcpIds = mcpIds.Select(g => (McpServerOptionsId)g).ToList();
        var mcps = await db.Mcps.Where(m => typedMcpIds.Contains(m.Id)).ToListAsync(ct);
        agent.McpServers.Clear();
        agent.McpServers.AddRange(mcps);
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignMiddlewareAsync(Guid agentId, IEnumerable<Guid> middlewareIds, CancellationToken ct = default)
    {
        var typedAgentId = (AgentOptionsId)agentId;
        var agent = await db.Agents.Include(a => a.Middleware).FirstOrDefaultAsync(a => a.Id == typedAgentId, ct);
        if (agent is null) return;
        var typedMiddlewareIds = middlewareIds.Select(g => (MiddlewareOptionsId)g).ToList();
        var middleware = await db.Middlewares.Where(m => typedMiddlewareIds.Contains(m.Id)).ToListAsync(ct);
        agent.Middleware.Clear();
        agent.Middleware.AddRange(middleware);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var typedId = (AgentOptionsId)id;
        var agent = await db.Agents.FindAsync([typedId], ct);
        if (agent is not null)
        {
            db.Agents.Remove(agent);
            await db.SaveChangesAsync(ct);
        }
    }
}
```

- [ ] **Step 2: Update `MemoryStore.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;

namespace Legion.Admin.Data.Stores;

public class MemoryStore(AppDbContext db)
{
    public async Task<List<MemoryOptions>> GetAllAsync(CancellationToken ct = default) =>
        await db.Memories.AsNoTracking().ToListAsync(ct);

    public async Task<MemoryOptions?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var typedId = (MemoryOptionsId)id;
        return await db.Memories.AsNoTracking().FirstOrDefaultAsync(m => m.Id == typedId, ct);
    }

    public async Task<MemoryOptions> AddAsync(MemoryOptions memory, CancellationToken ct = default)
    {
        db.Memories.Add(memory);
        await db.SaveChangesAsync(ct);
        return memory;
    }

    public async Task UpdateAsync(MemoryOptions memory, CancellationToken ct = default)
    {
        db.ChangeTracker.Clear();
        db.Memories.Update(memory);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var typedId = (MemoryOptionsId)id;
        var memory = await db.Memories.FindAsync([typedId], ct);
        if (memory is not null)
        {
            db.Memories.Remove(memory);
            await db.SaveChangesAsync(ct);
        }
    }
}
```

- [ ] **Step 3: Commit**

```bash
cd /home/timm/Legion
git add src/libs/Legion.Admin.Data/Stores/AgentStore.cs \
        src/libs/Legion.Admin.Data/Stores/MemoryStore.cs
git commit -m "refactor: AgentStore and MemoryStore use Guid IDs"
```

---

## Task 11: Update ISecretsStore interface and SecretsManager

**Files:**
- Modify: `src/libs/Legion.Admin.Data/Stores/SecretsStore.cs` (interface only)
- Modify: `src/libs/Legion.Admin.Data/Services/SecretsManager.cs`

- [ ] **Step 1: Update `SecretsStore.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;

namespace Legion.Admin.Data.Stores;

public interface ISecretsStore
{
    Task<List<SecretOptions>> GetAllAsync(CancellationToken ct = default);
    Task<SecretOptions?> FindByPathAsync(string path, CancellationToken ct = default);
    Task<List<SecretOptions>> GetChildrenAsync(string parentPath, CancellationToken ct = default);
    Task<SecretOptions> CreateAsync(string path, string? description, string plaintext, CancellationToken ct = default);
    Task UpdateValueAsync(Guid id, string plaintext, CancellationToken ct = default);
    Task UpdateDescriptionAsync(Guid id, string? description, CancellationToken ct = default);
    Task<string?> DecryptAsync(Guid id, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 2: Update `SecretsManager.cs`**

The existing `SecretsManager.ResolveAsync` calls `store.DecryptAsync(leaf.Id, ct)` where `leaf.Id` is now `SecretOptionsId`. This still works because `SecretOptionsId` implicitly converts to `Guid`. No changes needed to `SecretsManager.cs`.

Run: `dotnet build /home/timm/Legion/src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj`
Expected: This project may still fail because the Sqlite/PostgreSQL store implementations have not yet been updated. Continue.

- [ ] **Step 3: Commit**

```bash
cd /home/timm/Legion
git add src/libs/Legion.Admin.Data/Stores/SecretsStore.cs
git commit -m "refactor: ISecretsStore methods use Guid id"
```

---

## Task 12: Update SqliteSecretsStore and PostgreSqlSecretsStore

**Files:**
- Modify: `src/libs/Legion.Admin.Data.Sqlite/Stores/SqliteSecretsStore.cs`
- Modify: `src/libs/Legion.Admin.Data.PostgreSQL/Stores/PostgreSqlSecretsStore.cs`

- [ ] **Step 1: Update `SqliteSecretsStore.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Stores;
using Microsoft.EntityFrameworkCore;

namespace Legion.Admin.Data.Sqlite.Stores;

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
                     && !s.Path.Substring(prefix.Length).Contains('/'))
            .ToListAsync(ct);
    }

    public async Task<SecretOptions> CreateAsync(string path, string? description, string plaintext, CancellationToken ct = default)
    {
        var secret = new SecretOptions
        {
            Id = SecretOptionsId.New(),
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

    public async Task UpdateValueAsync(Guid id, string plaintext, CancellationToken ct = default)
    {
        var typedId = (SecretOptionsId)id;
        var secret = await db.Secrets.FindAsync([typedId], ct);
        if (secret is null) return;
        secret.EncryptedValue = plaintext;
        secret.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task UpdateDescriptionAsync(Guid id, string? description, CancellationToken ct = default)
    {
        var typedId = (SecretOptionsId)id;
        var secret = await db.Secrets.FindAsync([typedId], ct);
        if (secret is null) return;
        secret.Description = description;
        secret.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public Task<string?> DecryptAsync(Guid id, CancellationToken ct = default)
    {
        var typedId = (SecretOptionsId)id;
        return db.Secrets.AsNoTracking()
            .Where(s => s.Id == typedId)
            .Select(s => (string?)s.EncryptedValue)
            .FirstOrDefaultAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var typedId = (SecretOptionsId)id;
        var secret = await db.Secrets.FindAsync([typedId], ct);
        if (secret is not null)
        {
            db.Secrets.Remove(secret);
            await db.SaveChangesAsync(ct);
        }
    }
}
```

- [ ] **Step 2: Update `PostgreSqlSecretsStore.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Legion.Admin.Data.PostgreSQL.Stores;

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
        var newId = SecretOptionsId.New();
        await db.Database.ExecuteSqlRawAsync(
            """
            INSERT INTO "Secrets" ("Id", "Path", "Description", "EncryptedValue", "CreatedAt", "UpdatedAt")
            VALUES ({0}, {1}, {2}, pgp_sym_encrypt({3}, {4})::text, NOW(), NOW())
            """,
            [newId.Value, path, description as object ?? DBNull.Value, plaintext, key], ct);

        return await db.Secrets.AsNoTracking().FirstAsync(s => s.Path == path, ct);
    }

    public async Task UpdateValueAsync(Guid id, string plaintext, CancellationToken ct = default)
    {
        var key = EncryptionKey;
        await db.Database.ExecuteSqlRawAsync(
            """
            UPDATE "Secrets" SET "EncryptedValue" = pgp_sym_encrypt({0}, {1})::text, "UpdatedAt" = NOW()
            WHERE "Id" = {2}
            """,
            [plaintext, key, id], ct);
    }

    public async Task UpdateDescriptionAsync(Guid id, string? description, CancellationToken ct = default)
    {
        var typedId = (SecretOptionsId)id;
        var secret = await db.Secrets.FindAsync([typedId], ct);
        if (secret is null) return;
        secret.Description = description;
        secret.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<string?> DecryptAsync(Guid id, CancellationToken ct = default)
    {
        var key = EncryptionKey;
        var results = await db.Database
            .SqlQueryRaw<string>(
                """SELECT pgp_sym_decrypt("EncryptedValue"::bytea, {0}) AS "Value" FROM "Secrets" WHERE "Id" = {1}""",
                key, id)
            .ToListAsync(ct);
        return results.FirstOrDefault();
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var typedId = (SecretOptionsId)id;
        var secret = await db.Secrets.FindAsync([typedId], ct);
        if (secret is not null)
        {
            db.Secrets.Remove(secret);
            await db.SaveChangesAsync(ct);
        }
    }
}
```

- [ ] **Step 3: Build the data projects and note expected migration compile failures**

Run:
```bash
dotnet build /home/timm/Legion/src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj
dotnet build /home/timm/Legion/src/libs/Legion.Admin.Data.Sqlite/Legion.Admin.Data.Sqlite.csproj
dotnet build /home/timm/Legion/src/libs/Legion.Admin.Data.PostgreSQL/Legion.Admin.Data.PostgreSQL.csproj
```
Expected: `Legion.Admin.Data` builds. The provider projects may fail because existing migration files still reference integer keys; this is expected and fixed by deleting/regenerating App migrations in Tasks 15-16.

- [ ] **Step 4: Commit**

```bash
cd /home/timm/Legion
git add src/libs/Legion.Admin.Data.Sqlite/Stores/SqliteSecretsStore.cs \
        src/libs/Legion.Admin.Data.PostgreSQL/Stores/PostgreSqlSecretsStore.cs
git commit -m "refactor: secrets store implementations use Guid IDs

CreateAsync generates UUID v7 client-side; PostgreSQL stores Id explicitly
in INSERT (no SERIAL default)."
```

---

## Task 13: Update SecretsController, Razor pages, and UI form ID state

**Files:**
- Modify: `src/WebDev/Controllers/SecretsController.cs`
- Modify: `src/libs/Legion.Admin.UI/_Imports.razor`
- Modify: 18 Razor pages with `[Parameter] public int Id`
- Modify: 8 create pages that must assign UUID v7 IDs explicitly
- Modify: `src/libs/Legion.Admin.UI/Components/MemoryEditDialog.razor`

- [ ] **Step 1: Update `SecretsController.cs`**

Replace the file content with:

```csharp
using Legion.Admin.Data.Stores;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace WebDev.Controllers;

[ApiController, Route("api/secrets")]
[Authorize(Roles = "admin")]
public class SecretsController(ISecretsStore store) : ControllerBase
{
    [HttpPost("{id:guid}/reveal")]
    public async Task<IActionResult> Reveal(Guid id, CancellationToken ct = default)
    {
        var value = await store.DecryptAsync(id, ct);
        if (value is null) return NotFound();
        return Ok(new { value });
    }
}
```

- [ ] **Step 2: Update each route parameter page from `int` to `Guid`**

For each of the 18 files listed below, change `[Parameter] public int Id { get; set; }` to `[Parameter] public Guid Id { get; set; }`. Also update any route directive like `@page "/agents/{Id:int}/edit"` to `@page "/agents/{Id:guid}/edit"`.

Files (each contains exactly one `[Parameter] public int Id`; replace only that line and any matching `:int` route constraint):
1. `src/libs/Legion.Admin.UI/Pages/Agents/Agents.Update.razor`
2. `src/libs/Legion.Admin.UI/Pages/Agents/Agents.Delete.razor`
3. `src/libs/Legion.Admin.UI/Pages/Mcps/Mcps.Update.razor`
4. `src/libs/Legion.Admin.UI/Pages/Mcps/Mcps.Delete.razor`
5. `src/libs/Legion.Admin.UI/Pages/Memory/Memory.Update.razor`
6. `src/libs/Legion.Admin.UI/Pages/Memory/Memory.Delete.razor`
7. `src/libs/Legion.Admin.UI/Pages/Middleware/Middleware.Update.razor`
8. `src/libs/Legion.Admin.UI/Pages/Middleware/Middleware.Delete.razor`
9. `src/libs/Legion.Admin.UI/Pages/Models/Models.Update.razor`
10. `src/libs/Legion.Admin.UI/Pages/Models/Models.Delete.razor`
11. `src/libs/Legion.Admin.UI/Pages/Providers/Providers.Update.razor`
12. `src/libs/Legion.Admin.UI/Pages/Providers/Providers.Delete.razor`
13. `src/libs/Legion.Admin.UI/Pages/Skills/Skills.Update.razor`
14. `src/libs/Legion.Admin.UI/Pages/Skills/Skills.Delete.razor`
15. `src/libs/Legion.Admin.UI/Pages/Tools/Tools.Update.razor`
16. `src/libs/Legion.Admin.UI/Pages/Tools/Tools.Delete.razor`
17. `src/libs/Legion.Admin.UI/Pages/Workflows/Workflows.Update.razor`
18. `src/libs/Legion.Admin.UI/Pages/Workflows/Workflows.Delete.razor`

For each file, do:
- Replace `[Parameter] public int Id { get; set; }` with `[Parameter] public Guid Id { get; set; }`
- If the `@page` directive contains `{Id:int}`, change to `{Id:guid}`
- If the file contains a call like `Store.GetAsync(Id)` it still works because branded IDs implicitly convert from `Guid`

- [ ] **Step 3: Import the branded ID namespace for Razor**

In `src/libs/Legion.Admin.UI/_Imports.razor`, add this line with the other `Legion.Admin.Data.Models` imports:

```razor
@using Legion.Admin.Data.Models
```

- [ ] **Step 4: Update agent create/update form ID state**

In `src/libs/Legion.Admin.UI/Pages/Agents/Agents.Create.razor`:

Replace the selected ID fields:

```csharp
private IEnumerable<ToolOptionsId> selectedToolIds = [];
private IEnumerable<SkillOptionsId> selectedSkillIds = [];
private IEnumerable<ModelOptionsId> selectedModelIds = [];
private IEnumerable<McpServerOptionsId> selectedMcpIds = [];
private IEnumerable<MiddlewareOptionsId> selectedMiddlewareIds = [];
```

Replace the `FormModel.ProviderId` property:

```csharp
public ProviderOptionsId? ProviderId { get; set; }
```

Replace the `AgentOptions` object initializer in `HandleSubmit` so the agent gets a UUID v7 ID and no `Guid.Empty` provider fallback:

```csharp
var agent = new AgentOptions
{
    Id = AgentOptionsId.New(),
    Name = form.Name,
    MaxTokens = form.MaxTokens,
    Description = form.Description,
    Instructions = form.Instructions,
    ProviderId = form.ProviderId ?? throw new InvalidOperationException("Provider is required."),
};
```

Replace the assignment calls with `.Value` projections:

```csharp
if (selectedToolIds.Any())
    await AgentStore.AssignToolsAsync(saved.Id, selectedToolIds.Select(id => id.Value));
if (selectedSkillIds.Any())
    await AgentStore.AssignSkillsAsync(saved.Id, selectedSkillIds.Select(id => id.Value));
if (selectedModelIds.Any())
    await AgentStore.AssignModelsAsync(saved.Id, selectedModelIds.Select(id => id.Value));
if (selectedMcpIds.Any())
    await AgentStore.AssignMcpServersAsync(saved.Id, selectedMcpIds.Select(id => id.Value));
if (selectedMiddlewareIds.Any())
    await AgentStore.AssignMiddlewareAsync(saved.Id, selectedMiddlewareIds.Select(id => id.Value));
```

In `src/libs/Legion.Admin.UI/Pages/Agents/Agents.Update.razor`, make the same selected ID field and `FormModel.ProviderId` type replacements. Then replace these assignments:

```csharp
form.ProviderId = agent.ProviderId;
selectedToolIds = agent.Tools.Select(t => t.Id).ToList();
selectedSkillIds = agent.Skills.Select(s => s.Id).ToList();
selectedModelIds = agent.Models.Select(m => m.Id).ToList();
selectedMcpIds = agent.McpServers.Select(m => m.Id).ToList();
selectedMiddlewareIds = agent.Middleware.Select(m => m.Id).ToList();
```

Replace the `AgentOptions` object initializer in `HandleSubmit`:

```csharp
var agent = new AgentOptions
{
    Id = Id,
    Name = form.Name,
    MaxTokens = form.MaxTokens,
    Description = form.Description,
    Instructions = form.Instructions,
    ProviderId = form.ProviderId ?? throw new InvalidOperationException("Provider is required."),
};
```

Replace the assignment calls:

```csharp
await AgentStore.AssignToolsAsync(Id, selectedToolIds.Select(id => id.Value));
await AgentStore.AssignSkillsAsync(Id, selectedSkillIds.Select(id => id.Value));
await AgentStore.AssignModelsAsync(Id, selectedModelIds.Select(id => id.Value));
await AgentStore.AssignMcpServersAsync(Id, selectedMcpIds.Select(id => id.Value));
await AgentStore.AssignMiddlewareAsync(Id, selectedMiddlewareIds.Select(id => id.Value));
```

- [ ] **Step 5: Update provider create/update selected model state**

In `src/libs/Legion.Admin.UI/Pages/Providers/Providers.Create.razor` and `src/libs/Legion.Admin.UI/Pages/Providers/Providers.Update.razor`, replace:

```csharp
private IEnumerable<int> selectedModelIds = [];
```

with:

```csharp
private IEnumerable<ModelOptionsId> selectedModelIds = [];
```

In `Providers.Create.razor`, replace the `ProviderOptions` object initializer:

```csharp
var provider = new ProviderOptions
{
    Id = ProviderOptionsId.New(),
    Name = form.Name,
    ApiUrl = form.ApiUrl,
    ApiToken = form.ApiToken,
};
```

Replace the create-page assignment call:

```csharp
if (selectedModelIds.Any())
    await ProviderStore.AssignModelsAsync(saved.Id, selectedModelIds.Select(id => id.Value));
```

In `Providers.Update.razor`, replace:

```csharp
selectedModelIds = provider.Models.Select(m => m.Id).ToList();
```

and replace the update-page object initializer and assignment call:

```csharp
var provider = new ProviderOptions
{
    Id = Id,
    Name = form.Name,
    ApiUrl = form.ApiUrl,
    ApiToken = form.ApiToken,
};
await ProviderStore.UpdateAsync(provider);
await ProviderStore.AssignModelsAsync(Id, selectedModelIds.Select(id => id.Value));
```

- [ ] **Step 6: Assign UUID v7 IDs in all catalog create pages**

Update each create page object initializer:

`src/libs/Legion.Admin.UI/Pages/Tools/Tools.Create.razor`:

```csharp
var tool = new ToolOptions
{
    Id = ToolOptionsId.New(),
    Name = form.Name,
    Description = form.Description,
    ParametersSchema = form.ParametersSchema,
};
```

`src/libs/Legion.Admin.UI/Pages/Skills/Skills.Create.razor`:

```csharp
var skill = new SkillOptions
{
    Id = SkillOptionsId.New(),
    Name = form.Name,
    Description = form.Description,
    License = form.License,
    Compatibility = form.Compatibility,
    AllowedTools = form.AllowedTools,
    Content = form.Content,
    Resources = form.Resources,
    Scripts = form.Scripts,
};
```

`src/libs/Legion.Admin.UI/Pages/Workflows/Workflows.Create.razor`:

```csharp
var workflow = new WorkflowOptions
{
    Id = WorkflowOptionsId.New(),
    Name = form.Name,
    Description = form.Description,
    Content = form.Content,
};
```

`src/libs/Legion.Admin.UI/Pages/Middleware/Middleware.Create.razor`:

```csharp
var middleware = new MiddlewareOptions
{
    Id = MiddlewareOptionsId.New(),
    Name = form.Name,
    Description = form.Description,
    Scope = form.Scope,
    Type = form.Type,
    Content = form.Content,
};
```

`src/libs/Legion.Admin.UI/Pages/Models/Models.Create.razor`: add `Id = ModelOptionsId.New(),` as the first property in the existing `new ModelOptions` initializer. Keep every existing model metadata property unchanged.

`src/libs/Legion.Admin.UI/Pages/Mcps/Mcps.Create.razor`: replace the object initializer:

```csharp
var mcp = new McpServerOptions
{
    Id = McpServerOptionsId.New(),
    Name = form.Name,
    Description = form.Description,
    ServerLabel = form.ServerLabel,
    ServerUrl = form.ServerUrl,
    CommandLine = form.CommandLine,
    Transport = form.Transport,
    RequireApproval = form.RequireApproval,
    Headers = headers.Select(h => new McpServerHeaders
    {
        Id = McpServerHeadersId.New(),
        Key = h.Key,
        Value = h.Value
    }).ToList(),
};
```

In `src/libs/Legion.Admin.UI/Pages/Mcps/Mcps.Update.razor`, replace the `ReplaceHeadersAsync` call with:

```csharp
await McpStore.ReplaceHeadersAsync(Id,
    headers.Select(h => new McpServerHeaders
    {
        Id = McpServerHeadersId.New(),
        Key = h.Key,
        Value = h.Value
    }).ToList());
```

- [ ] **Step 7: Update `MemoryEditDialog.razor`**

In `src/libs/Legion.Admin.UI/Components/MemoryEditDialog.razor`, replace:

```csharp
[Parameter] public int AgentId { get; set; }
```

with:

```csharp
[Parameter] public Guid AgentId { get; set; }
```

Replace the memory object initializer IDs:

```csharp
var memory = new MemoryOptions
{
    Id = ExistingMemory?.Id ?? MemoryOptionsId.New(),
    AgentId = AgentId,
    SearchTime = form.SearchTime,
    MaxResults = form.MaxResults,
    FunctionToolName = form.FunctionToolName,
    FunctionToolDescription = form.FunctionToolDescription,
    ContextPrompt = form.ContextPrompt,
    StateKey = form.StateKey,
};
```

- [ ] **Step 8: Build the UI project**

Run: `dotnet build /home/timm/Legion/src/libs/Legion.Admin.UI/Legion.Admin.UI.csproj`
Expected: Build succeeds.

- [ ] **Step 9: Commit**

```bash
cd /home/timm/Legion
git add src/WebDev/Controllers/SecretsController.cs \
        src/libs/Legion.Admin.UI/_Imports.razor \
        src/libs/Legion.Admin.UI/Pages/ \
        src/libs/Legion.Admin.UI/Components/MemoryEditDialog.razor
git commit -m "refactor: UI uses Guid routes and branded ID form state

Route parameters now bind as Guid. Create forms assign UUID v7 branded IDs
explicitly, multi-select values use branded ID collections, and assignment
store calls project selected IDs back to Guid values."
```

---

## Task 14: Update SeedData.Agents and seed call sites

**Files:**
- Modify: `src/libs/Legion.Admin.Data/Seeds/SeedData.Agents.cs`

- [ ] **Step 1: Update `SeedData.Agents.cs`**

The existing `GetDefaultAgents()` returns a list with no explicit `Id` — it relied on int autogeneration. With UUID v7 we generate the ID explicitly. Replace the file content with:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;

namespace Legion.Admin.Data.Seeds;

internal static partial class SeedData
{
    public static List<AgentOptions> GetDefaultAgents() => new ()
    {
        new ()
        {
            Id = AgentOptionsId.New(),
            Name = "Default Agent",
        }
    };
}
```

- [ ] **Step 2: Commit**

```bash
cd /home/timm/Legion
git add src/libs/Legion.Admin.Data/Seeds/SeedData.Agents.cs
git commit -m "refactor: seed agent uses AgentOptionsId.New()"
```

---

## Task 15: Delete old App migrations

**Files:**
- Delete: `src/libs/Legion.Admin.Data.PostgreSQL/Migrations/App/*.cs`
- Delete: `src/libs/Legion.Admin.Data.Sqlite/Migrations/App/*.cs`

Auth migrations are kept untouched.

- [ ] **Step 1: Delete PostgreSQL App migrations**

Run:
```bash
rm /home/timm/Legion/src/libs/Legion.Admin.Data.PostgreSQL/Migrations/App/20260427121325_InitialCreate.Designer.cs
rm /home/timm/Legion/src/libs/Legion.Admin.Data.PostgreSQL/Migrations/App/20260427121325_InitialCreate.cs
rm /home/timm/Legion/src/libs/Legion.Admin.Data.PostgreSQL/Migrations/App/20260428101441_AddSecrets.Designer.cs
rm /home/timm/Legion/src/libs/Legion.Admin.Data.PostgreSQL/Migrations/App/20260428101441_AddSecrets.cs
rm /home/timm/Legion/src/libs/Legion.Admin.Data.PostgreSQL/Migrations/App/AppDbContextModelSnapshot.cs
```

- [ ] **Step 2: Delete SQLite App migrations**

Run:
```bash
rm /home/timm/Legion/src/libs/Legion.Admin.Data.Sqlite/Migrations/App/20260427122158_InitialCreate.Designer.cs
rm /home/timm/Legion/src/libs/Legion.Admin.Data.Sqlite/Migrations/App/20260427122158_InitialCreate.cs
rm /home/timm/Legion/src/libs/Legion.Admin.Data.Sqlite/Migrations/App/AppDbContextModelSnapshot.cs
rm /home/timm/Legion/src/libs/Legion.Admin.Data.Sqlite/Migrations/20260428101256_AddSecrets.Designer.cs
rm /home/timm/Legion/src/libs/Legion.Admin.Data.Sqlite/Migrations/20260428101256_AddSecrets.cs
```

Note: Some SQLite migrations live at `Migrations/` root rather than `Migrations/App/` — check both locations.

- [ ] **Step 3: Confirm Auth migrations remain**

Run: `find /home/timm/Legion/src/libs/Legion.Admin.Data.PostgreSQL/Migrations/Auth /home/timm/Legion/src/libs/Legion.Admin.Data.Sqlite/Migrations/Auth -name "*.cs" 2>/dev/null`
Expected: Auth migration files still exist for both providers.

- [ ] **Step 4: Build (expecting failure)**

Run: `dotnet build /home/timm/Legion/Legion.sln`
Expected: Build may fail because EF Core needs a model snapshot. The next task regenerates them.

- [ ] **Step 5: Commit**

```bash
cd /home/timm/Legion
git add -A src/libs/Legion.Admin.Data.PostgreSQL/Migrations/ \
          src/libs/Legion.Admin.Data.Sqlite/Migrations/
git commit -m "chore: delete App migrations to regenerate against branded IDs

Auth migrations untouched. Fresh InitialCreate migration generated next."
```

---

## Task 16: Regenerate App migrations for both providers

**Files:**
- Regenerated: `src/libs/Legion.Admin.Data.PostgreSQL/Migrations/App/<timestamp>_InitialCreate.cs`
- Regenerated: `src/libs/Legion.Admin.Data.Sqlite/Migrations/App/<timestamp>_InitialCreate.cs`

- [ ] **Step 1: Verify dotnet ef tool is installed**

Run: `dotnet ef --version`
Expected: A version string is printed. If not, run `dotnet tool install --global dotnet-ef --version 10.*` first.

- [ ] **Step 2: Generate PostgreSQL App migration**

Run from the repo root:
```bash
cd /home/timm/Legion
dotnet ef migrations add InitialCreate \
  --project src/libs/Legion.Admin.Data.PostgreSQL/Legion.Admin.Data.PostgreSQL.csproj \
  --startup-project src/libs/Legion.Admin.Data.PostgreSQL/Legion.Admin.Data.PostgreSQL.csproj \
  --context AppDbContext \
  --output-dir Migrations/App
```
Expected: New migration file created in `src/libs/Legion.Admin.Data.PostgreSQL/Migrations/App/`.

If this fails because `Legion.Admin.Data.PostgreSQL` lacks a startup, use the design-time factory at `src/libs/Legion.Admin.Data.PostgreSQL/AppDbDesignTimeFactory.cs` (already exists per repo inspection).

- [ ] **Step 3: Generate SQLite App migration**

Run:
```bash
cd /home/timm/Legion
dotnet ef migrations add InitialCreate \
  --project src/libs/Legion.Admin.Data.Sqlite/Legion.Admin.Data.Sqlite.csproj \
  --startup-project src/libs/Legion.Admin.Data.Sqlite/Legion.Admin.Data.Sqlite.csproj \
  --context AppDbContext \
  --output-dir Migrations/App
```
Expected: New migration file created in `src/libs/Legion.Admin.Data.Sqlite/Migrations/App/`.

- [ ] **Step 4: Inspect generated migrations**

Run: `find /home/timm/Legion/src/libs/Legion.Admin.Data.PostgreSQL/Migrations/App /home/timm/Legion/src/libs/Legion.Admin.Data.Sqlite/Migrations/App -name "*InitialCreate.cs" -exec grep -l "uuid\|TEXT" {} \;`

Open the PostgreSQL migration and confirm:
- All ID columns use `uuid` type (not `integer` / `serial`)
- All FK columns are `uuid`
- Junction tables (`AgentMcpServers`, `AgentMiddleware`, `AgentModels`, `AgentTools`, `AgentSkills`, `ProviderModels`) have `uuid` FK columns

Open the SQLite migration and confirm:
- All ID columns use `TEXT` (SQLite stores GUIDs as strings)
- All FK columns are `TEXT`

- [ ] **Step 5: Build the entire solution**

Run: `dotnet build /home/timm/Legion/Legion.sln`
Expected: Build succeeds.

- [ ] **Step 6: Commit**

```bash
cd /home/timm/Legion
git add src/libs/Legion.Admin.Data.PostgreSQL/Migrations/App/ \
        src/libs/Legion.Admin.Data.Sqlite/Migrations/App/
git commit -m "feat: regenerate App migrations against UUID v7 branded IDs

Fresh InitialCreate migrations for PostgreSQL and SQLite. All PK and FK
columns are uuid (Postgres) / TEXT (SQLite). Auth migrations untouched."
```

---

## Task 17: Update existing tests

**Files:**
- Modify: `tests/Legion.Secrets.Tests/SqliteSecretsStoreTests.cs`
- Modify: `tests/Legion.Secrets.Tests/SecretsManagerTests.cs`
- Modify: `tests/Legion.Secrets.Tests/SecretResolvingHandlerTests.cs`
- Modify: `tests/Legion.Secrets.Tests/SecretsControllerTests.cs`

- [ ] **Step 1: Read existing test files to identify int IDs**

Run: `rg -n "Arg\.Any<int>|It\.IsAny<int>|\bint\s+\w*id\b|Id\s*=\s*[0-9]+|DecryptAsync\(|UpdateValueAsync\(|UpdateDescriptionAsync\(|DeleteAsync\(" /home/timm/Legion/tests/Legion.Secrets.Tests`
Expected: Lists every place where tests still use explicit integer IDs or mock/store methods that now take `Guid`.

- [ ] **Step 2: Update tests to compile against new types**

For each test file:
- Any local `int id` declaration → `Guid id`
- Any `secret.Id` usage continues to compile because `SecretOptionsId` implicitly converts to `Guid` when passed to `DecryptAsync(Guid id, ...)`
- Mocked `ISecretsStore.DecryptAsync(It.IsAny<int>(), ...)` (NSubstitute: `store.DecryptAsync(Arg.Any<int>(), ...)`) → `Arg.Any<Guid>()`

In `SqliteSecretsStoreTests.cs`, the existing test uses `secret.Id` returned from `CreateAsync` — that now returns `SecretOptionsId`. Since the store methods take `Guid`, the implicit conversion works without changes. Look for any explicit `int` type annotations in test variables and change to `Guid` (or remove the annotation in favor of `var`).

In `SecretsManagerTests.cs` and `SecretResolvingHandlerTests.cs`: search for `Arg.Any<int>()` and replace with `Arg.Any<Guid>()`. Search for any constructor like `new SecretOptions { Id = 1, ... }` and replace with `new SecretOptions { Id = SecretOptionsId.New(), ... }`.

In `SecretsControllerTests.cs`: search for any explicit `int id = ...` in test setup and replace with `Guid id = Guid.NewGuid();` (or `SecretOptionsId.New().Value`).

- [ ] **Step 3: Run all tests**

Run: `dotnet test /home/timm/Legion/tests/Legion.Secrets.Tests/Legion.Secrets.Tests.csproj`
Expected: All tests pass. If any fail, the failure message should make the fix obvious — adjust types accordingly.

- [ ] **Step 4: Commit**

```bash
cd /home/timm/Legion
git add tests/Legion.Secrets.Tests/
git commit -m "test: update secrets tests for branded ID types

NSubstitute Arg.Any<int>() → Arg.Any<Guid>(); explicit int IDs replaced
with SecretOptionsId.New() or Guid.NewGuid()."
```

---

## Task 18: Add a focused test for branded ID round-tripping

This single regression test confirms the value converters work end-to-end against a real SQLite database.

**Files:**
- Create: `tests/Legion.Secrets.Tests/BrandedIdRoundTripTests.cs`

- [ ] **Step 1: Write the test**

Create `tests/Legion.Secrets.Tests/BrandedIdRoundTripTests.cs`:

```csharp
using Legion.Admin.Data;
using Legion.Admin.Data.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Legion.Secrets.Tests;

public class BrandedIdRoundTripTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AppDbContext _db;

    public BrandedIdRoundTripTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .Options;
        _db = new AppDbContext(options);
        _db.Database.EnsureCreated();
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task SecretOptionsId_RoundTripsThroughDatabase()
    {
        var id = SecretOptionsId.New();
        _db.Secrets.Add(new SecretOptions
        {
            Id = id,
            Path = "test/path",
            EncryptedValue = "value"
        });
        await _db.SaveChangesAsync();

        _db.ChangeTracker.Clear();
        var retrieved = await _db.Secrets.FirstAsync(s => s.Path == "test/path");

        Assert.Equal(id, retrieved.Id);
        Assert.Equal(id.Value, retrieved.Id.Value);
    }

    [Fact]
    public void SecretOptionsId_GuidConversionsAreImplicit()
    {
        var typed = SecretOptionsId.New();

        Guid asGuid = typed;          // implicit operator
        SecretOptionsId backToTyped = asGuid;

        Assert.Equal(typed.Value, asGuid);
        Assert.Equal(typed, backToTyped);
    }

    [Fact]
    public void New_GeneratesUuidV7()
    {
        var id = SecretOptionsId.New();

        Assert.Equal(7, id.Value.Version);
    }
}
```

- [ ] **Step 2: Run the new test**

Run: `dotnet test /home/timm/Legion/tests/Legion.Secrets.Tests/Legion.Secrets.Tests.csproj --filter FullyQualifiedName~BrandedIdRoundTripTests`
Expected: All three tests pass.

- [ ] **Step 3: Run full test suite**

Run: `dotnet test /home/timm/Legion/tests/Legion.Secrets.Tests/Legion.Secrets.Tests.csproj`
Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
cd /home/timm/Legion
git add tests/Legion.Secrets.Tests/BrandedIdRoundTripTests.cs
git commit -m "test: add branded ID round-trip and UUID v7 verification"
```

---

## Task 19: Smoke-test WebDev startup

**Files:**
- None (runtime verification only)

- [ ] **Step 1: Delete any existing dev SQLite databases**

Run: `find /home/timm/Legion -maxdepth 3 -name "*.db" -not -path "*/obj/*" -not -path "*/bin/*"`
If files appear, remove them: `rm <path>`. (These are transient dev databases that would conflict with the regenerated schema.)

- [ ] **Step 2: Build the WebDev project**

Run: `dotnet build /home/timm/Legion/src/WebDev/WebDev.csproj`
Expected: Build succeeds with no errors.

- [ ] **Step 3: Start WebDev briefly to confirm migration applies**

Run (with 30-second timeout):
```bash
cd /home/timm/Legion/src/WebDev
timeout 30 dotnet run --no-build 2>&1 | head -40
```
Expected: Output shows "Now listening on…" with no exception during DB migration. If you see a migration exception, inspect the message for the failing column type — the most likely cause is a missed value converter in Task 7.

- [ ] **Step 4: Confirm tables exist with correct types**

If WebDev started, the SQLite database has been created. Inspect it:
```bash
sqlite3 /home/timm/Legion/src/WebDev/agent.db ".schema Secrets" 2>/dev/null
```
Expected: The `Id` column is `TEXT NOT NULL` (SQLite GUID storage), not `INTEGER`.

- [ ] **Step 5: Commit (no changes — just markers if any)**

If `Step 1` removed any dev databases, no commit is needed. If you discovered and fixed a missed converter in `Step 3`, commit that fix:
```bash
cd /home/timm/Legion
git status
# if changes are present:
git add -A
git commit -m "fix: address smoke-test runtime issue"
```

---

## Task 20: Final verification

**Files:**
- None (verification only)

- [ ] **Step 1: Confirm full solution builds clean**

Run: `dotnet build /home/timm/Legion/Legion.sln 2>&1 | tail -10`
Expected: `Build succeeded` with no errors.

- [ ] **Step 2: Confirm all tests pass**

Run: `dotnet test /home/timm/Legion/tests/Legion.Secrets.Tests/Legion.Secrets.Tests.csproj 2>&1 | tail -5`
Expected: All tests pass; total count matches Task 1 baseline plus 3 new tests from Task 18.

- [ ] **Step 3: Audit remaining `int` ID references**

Run:
```bash
grep -rnE "int Id\b|public int.*Id\b|IEnumerable<int>.*Id|List<int>.*Id|int\? ProviderId|Arg\.Any<int>|It\.IsAny<int>" /home/timm/Legion/src /home/timm/Legion/tests \
  --include="*.cs" --include="*.razor" 2>/dev/null \
  | grep -v Migrations | grep -v obj | grep -v bin | grep -v ".external"
```
Expected: No matches in `Models/`, `Stores`, `Configurations`, controllers, Razor pages/components, or tests. Acceptable matches: Auth-related files only.

- [ ] **Step 4: Verify Auth migrations untouched**

Run: `git log --oneline -- src/libs/Legion.Admin.Data.PostgreSQL/Migrations/Auth/ src/libs/Legion.Admin.Data.Sqlite/Migrations/Auth/`
Expected: No commits from this work touched Auth migrations — only the original `InitialCreate` Auth commit.

- [ ] **Step 5: Final commit (if anything caught above)**

If audits in Step 3 found stragglers, fix them and commit:
```bash
cd /home/timm/Legion
git add -A
git commit -m "fix: address remaining int Id stragglers found during audit"
```

If the audit was clean, no commit needed.
