# Seed System Refactor + Provider Secret FK Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `ProviderOptions.ApiToken` plaintext string with an optional FK to `SecretOptions`, and replace the YAML seed loader's switch/case dispatch with a type-discriminated deserializer that lets Admin models (Provider, Secret, Agent) deserialize directly from YAML.

**Architecture:**
1. **Model layer:** `ProviderOptions.ApiToken` (string) → `ApiTokenSecretId` (SecretOptionsId?) + `ApiTokenSecret` (SecretOptions?) navigation. A `[NotMapped]` `ApiTokenSecretPath` string on `ProviderOptions` carries the YAML linkage and is resolved to the FK during seeding.
2. **Seed layer:** New YAML format uses a flat `entities:` list, each item tagged with `seedType: <discriminator>`. `YamlDotNet`'s `WithTypeDiscriminatingNodeDeserializer<ISeedEntity>` dispatches to the right concrete type (Admin models direct, Auth models via DTO). `AdminDbSeedService` runs two passes: persist secrets first, then resolve FK paths/names, then persist providers and agents.
3. **FK resolution mode:** Strict — throw on dangling references in dev. UI Provider Create/Update pages switch from the inline `ApiToken` text input to a dropdown of existing `SecretOptions` (by `Path`).

**Tech Stack:** .NET 10, EF Core (Sqlite + PostgreSQL providers), YamlDotNet 16.3.0, xUnit (no FluentAssertions), Radzen Blazor.

**Worktree:** `.worktrees/seed-refactor` on branch `feature/seed-system-refactor`.

---

## Conventions

- Tests use xUnit with bare `Assert.*` (no FluentAssertions in this repo).
- IDs: `readonly record struct XxxId(Guid Value)` with implicit conversions to/from `Guid`. Use `XxxId.New()` to mint.
- Migration generation: from each provider directory, run `./update-migrations.sh <Name>`. Each script builds the project then runs `dotnet ef migrations add` for both `AppDbContext` and `AuthDbContext`.
- Run a focused test class with: `dotnet test tests/Legion.Admin.Data.Tests/Legion.Admin.Data.Tests.csproj --filter "FullyQualifiedName~<TestClass>" --nologo`.
- Build the solution: `dotnet build Legion.slnx --nologo`.

---

## File Structure

**Created**
- `src/libs/Legion.Admin.Data/Seeds/ISeedEntity.cs` — marker interface for type-discriminated entities.
- `src/libs/Legion.Admin.Data/Seeds/Dtos/ProviderSeedFields.cs` — extension/partial bag of `[YamlMember]` linking fields if not added directly to model. *(Decision below: add directly to ProviderOptions as `[NotMapped]`.)*
- `src/libs/Legion.Admin.Data/Seeds/SeedEntityRegistry.cs` — central dictionary mapping `seedType` discriminator → `Type`.
- `src/WebDev/seed/secrets.yml` — new seed file with the Anthropic API token secret.
- `src/WebDev/seed/providers.yml` — new seed file with the Anthropic provider linked to the secret.
- Migration files: `src/libs/Legion.Admin.Data.Sqlite/Migrations/App/<timestamp>_AddProviderApiTokenSecretFk.{cs,Designer.cs}` and PostgreSQL counterpart.

**Modified**
- `src/libs/Legion.Admin.Data.Models/Providers/ProviderOptions.cs` — drop `ApiToken`; add `ApiTokenSecretId`, `ApiTokenSecret`, `ApiTokenSecretPath` ([NotMapped]); implement `ISeedEntity`.
- `src/libs/Legion.Admin.Data.Models/SecretOptions.cs` — implement `ISeedEntity`.
- `src/libs/Legion.Admin.Data/Configurations/ProviderOptionsConfiguration.cs` — add optional FK to `SecretOptions`; ignore `ApiTokenSecretPath`.
- `src/libs/Legion.Admin.Data/Stores/ProviderStore.cs` — `Include(p => p.ApiTokenSecret)` in queries.
- `src/libs/Legion.Admin.Data/Seeds/SeedPayload.cs` — add `Secrets`, `Providers`; keep `Agents` (now `AgentOptions` instead of DTO), Users, OidcApplications, OidcScopes.
- `src/libs/Legion.Admin.Data/Seeds/Dtos/SeedAgentDto.cs` — *delete*; `AgentOptions` deserialized directly. Add `[NotMapped] string? ProviderName` to `AgentOptions` for linking.
- `src/libs/Legion.Admin.Data/Seeds/YamlSeedLoader.cs` — replace switch/case with type-discriminated `entities:` list deserializer.
- `src/libs/Legion.Admin.Data/Services/AdminDbSeedService.cs` — two-pass loading: persist secrets, resolve FKs, persist providers + agents.
- `src/libs/Legion.Admin.Data/Services/AuthDbSeedService.cs` — read DTOs from `SeedPayload` as-is (they're populated via discriminator now).
- `src/libs/Legion.Admin.UI/Pages/Providers/Providers.Create.razor` — secret dropdown.
- `src/libs/Legion.Admin.UI/Pages/Providers/Providers.Update.razor` — secret dropdown.
- `src/WebDev/seed/agents.yml`, `users.yml`, `oidc-applications.yml`, `oidc-scopes.yml` — migrate to `entities:` format.
- `tests/Legion.Admin.Data.Tests/Seeds/YamlSeedLoaderTests.cs` — rewrite to new YAML format, add discriminator tests, add provider+secret tests, add strict-FK-error test.
- `tests/Legion.Admin.Data.Tests/Seeds/YamlSeedIntegrationTests.cs` — update to new format.

**Deleted**
- `src/libs/Legion.Admin.Data/Seeds/Dtos/SeedAgentDto.cs` (replaced by direct `AgentOptions` deserialization).

---

## Task 1: Add `ISeedEntity` marker and registry

**Files:**
- Create: `src/libs/Legion.Admin.Data/Seeds/ISeedEntity.cs`
- Create: `src/libs/Legion.Admin.Data/Seeds/SeedEntityRegistry.cs`

- [ ] **Step 1: Create the marker interface**

```csharp
// src/libs/Legion.Admin.Data/Seeds/ISeedEntity.cs
namespace Legion.Admin.Data.Seeds;

// Marker interface for any entity that can appear in a seed YAML `entities:` list.
public interface ISeedEntity { }
```

- [ ] **Step 2: Create the seed-type registry**

```csharp
// src/libs/Legion.Admin.Data/Seeds/SeedEntityRegistry.cs
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Legion.Admin.Data.Models.Providers;
using Legion.Admin.Data.Seeds.Dtos;

namespace Legion.Admin.Data.Seeds;

public static class SeedEntityRegistry
{
    // YAML `seedType` value -> CLR type. Adding a new seedable type means adding one line here.
    public static readonly IReadOnlyDictionary<string, Type> Map = new Dictionary<string, Type>
    {
        ["secret"] = typeof(SecretOptions),
        ["provider"] = typeof(ProviderOptions),
        ["agent"] = typeof(AgentOptions),
        ["user"] = typeof(SeedUserDto),
        ["oidc-application"] = typeof(OidcApplicationDto),
        ["oidc-scope"] = typeof(OidcScopeDto),
    };
}
```

- [ ] **Step 3: Build to verify compile (interface unused yet, so compiles)**

Run: `dotnet build src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj --nologo`
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/libs/Legion.Admin.Data/Seeds/ISeedEntity.cs src/libs/Legion.Admin.Data/Seeds/SeedEntityRegistry.cs
git commit -m "feat(seed): add ISeedEntity marker and SeedEntityRegistry"
```

---

## Task 2: Refactor `ProviderOptions` to FK + linking field

**Files:**
- Modify: `src/libs/Legion.Admin.Data.Models/Providers/ProviderOptions.cs`
- Modify: `src/libs/Legion.Admin.Data.Models/SecretOptions.cs`
- Modify: `src/libs/Legion.Admin.Data/Configurations/ProviderOptionsConfiguration.cs`

- [ ] **Step 1: Replace `ApiToken` with FK fields and implement `ISeedEntity`**

`src/libs/Legion.Admin.Data.Models/Providers/ProviderOptions.cs`:

```csharp
using System.ComponentModel.DataAnnotations.Schema;
using Legion.Admin.Data.Models.Agents;
using Legion.Admin.Data.Seeds;

namespace Legion.Admin.Data.Models.Providers;

public enum ProviderType
{
    Anthropic,
    Ollama,
    OpenAI,
    AzureOpenAI,
    MicrosoftFoundry,
    HuggingFace,
    GithubCopilot,
    CopilotStudio,
    Cloudflare,
    Custom
}

public record ProviderOptions : ISeedEntity
{
    public ProviderOptionsId Id { get; init; }
    public string? Name { get; init; }
    public ProviderType Type { get; init; }
    public string? ApiUrl { get; init; }

    public SecretOptionsId? ApiTokenSecretId { get; set; }
    public SecretOptions? ApiTokenSecret { get; set; }

    // Seed-only linking field. Populated from YAML; resolved to ApiTokenSecretId by
    // AdminDbSeedService and not persisted to the database.
    [NotMapped]
    public string? ApiTokenSecretPath { get; set; }

    public List<AgentOptions> Agents { get; set; } = [];
    public List<ModelOptions> Models { get; set; } = [];
}
```

- [ ] **Step 2: Make `SecretOptions` an `ISeedEntity`**

Edit `src/libs/Legion.Admin.Data.Models/SecretOptions.cs`:

```csharp
using Legion.Admin.Data.Seeds;

namespace Legion.Admin.Data.Models;

public record SecretOptions : ISeedEntity
{
    public SecretOptionsId Id { get; init; }
    public string Path { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EncryptedValue { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
```

- [ ] **Step 3: Add the optional FK and ignore the `[NotMapped]` field in EF**

`src/libs/Legion.Admin.Data/Configurations/ProviderOptionsConfiguration.cs`:

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

        builder.Property(p => p.ApiTokenSecretId)
            .HasConversion(
                id => id.HasValue ? id.Value.Value : (Guid?)null,
                value => value.HasValue ? new SecretOptionsId(value.Value) : null);

        builder.HasOne(p => p.ApiTokenSecret)
            .WithMany()
            .HasForeignKey(p => p.ApiTokenSecretId)
            .OnDelete(DeleteBehavior.SetNull)
            .IsRequired(false);

        builder.Ignore(p => p.ApiTokenSecretPath);

        builder.HasMany(p => p.Models)
            .WithMany(m => m.Providers)
            .UsingEntity(t => t.ToTable("ProviderModels", schema: Schema));
    }
}
```

- [ ] **Step 4: Make `AgentOptions` implement `ISeedEntity` and add seeding linkage**

Edit `src/libs/Legion.Admin.Data.Models/Agents/AgentOptions.cs` — add `ISeedEntity` to the declaration and a `[NotMapped] string? ProviderName` for seed-time linking:

```csharp
using System.ComponentModel.DataAnnotations.Schema;
using Legion.Admin.Data.Models.Providers;
using Legion.Admin.Data.Seeds;

namespace Legion.Admin.Data.Models.Agents;

public record AgentOptions : ISeedEntity
{
    public AgentOptionsId Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Instructions { get; init; }
    public int? MaxTokens { get; init; }
    public ProviderOptionsId ProviderId { get; set; }
    public ProviderOptions? Provider { get; set; }

    [NotMapped]
    public string? ProviderName { get; set; }

    public MemoryOptions? Memory { get; set; }
    public List<ModelOptions> Models { get; set; } = [];
    public List<SkillOptions> Skills { get; set; } = [];
    public List<ToolOptions> Tools { get; set; } = [];
    public List<McpServerOptions> McpServers { get; set; } = [];
    public List<MiddlewareOptions> Middleware { get; set; } = [];
}
```

Add `builder.Ignore(a => a.ProviderName);` to the existing `AgentOptionsConfiguration` (find with `find src -name AgentOptionsConfiguration.cs`).

- [ ] **Step 5: Make Auth seed DTOs implement `ISeedEntity`**

Edit:
- `src/libs/Legion.Admin.Data/Seeds/Dtos/SeedUserDto.cs`: change to `public record SeedUserDto : ISeedEntity` and `using Legion.Admin.Data.Seeds;` (already in namespace, just add the marker).
- `src/libs/Legion.Admin.Data/Seeds/Dtos/OidcApplicationDto.cs`: `public record OidcApplicationDto : ISeedEntity`.
- `src/libs/Legion.Admin.Data/Seeds/Dtos/OidcScopeDto.cs`: `public record OidcScopeDto : ISeedEntity`.

(All three DTO files already live in `Legion.Admin.Data.Seeds.Dtos`, which is in the same assembly as `ISeedEntity`. No `using` needed since the marker is in the parent namespace; if compiler complains, add `using Legion.Admin.Data.Seeds;`.)

- [ ] **Step 6: Build (will fail — `ApiToken` references in UI/store)**

Run: `dotnet build Legion.slnx --nologo 2>&1 | grep -E "error CS" | head`
Expected errors in `Providers.Create.razor`, `Providers.Update.razor`, possibly `ProviderStore.cs`. Note them; they are fixed in Task 3 and Task 5.

- [ ] **Step 7: Commit (broken build is intentional checkpoint)**

Skip commit — leave uncommitted; we'll bundle with Task 3/5 since broken build can't ship.

---

## Task 3: Update `ProviderStore` and Provider UI to use FK

**Files:**
- Modify: `src/libs/Legion.Admin.Data/Stores/ProviderStore.cs`
- Modify: `src/libs/Legion.Admin.UI/Pages/Providers/Providers.Create.razor`
- Modify: `src/libs/Legion.Admin.UI/Pages/Providers/Providers.Update.razor`

- [ ] **Step 1: Add `Include(p => p.ApiTokenSecret)` to `ProviderStore`**

`src/libs/Legion.Admin.Data/Stores/ProviderStore.cs`:

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
        Db.Providers.AsNoTracking()
            .Include(p => p.Models)
            .Include(p => p.ApiTokenSecret);

    public override async Task<ProviderOptions?> GetAsync(Guid id, CancellationToken ct = default) =>
        await Db.Providers.AsNoTracking()
            .Include(p => p.Models)
            .Include(p => p.ApiTokenSecret)
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

- [ ] **Step 2: Replace `ApiToken` field in `Providers.Update.razor` with a secret picker**

Replace the entire "API Token" `RadzenRow` (the one with `apiTokenSecretMode` toggle, ~lines 36–60) and `FormModel` to use `SecretOptionsId?` linkage. Replace the relevant pieces:

In the markup section, replace the API-token row:

```razor
<RadzenRow>
    <RadzenColumn Size="4" SizeMD="4" class="rz-text-align-start rz-text-align-md-end">
        <RadzenLabel Text="API Token Secret" />
    </RadzenColumn>
    <RadzenColumn Size="8" SizeMD="8" Style="display: block; width: 100%;">
        <RadzenDropDown Name="ApiTokenSecret" @bind-Value="form.ApiTokenSecretId"
                        Data="allSecrets" TextProperty="Path" ValueProperty="Id"
                        AllowClear="true" Placeholder="(none — no API token)"
                        style="width: 100%;" />
        <RadzenDataAnnotationValidator Component="ApiTokenSecret" Style="position: absolute" />
    </RadzenColumn>
</RadzenRow>
```

Replace the `OnInitializedAsync`, `HandleSubmit`, and `FormModel` blocks:

```csharp
@code {
    [Parameter] public Guid Id { get; set; }

    private readonly FormModel form = new();
    private List<ModelOptions> allModels = [];
    private List<SecretOptions> allSecrets = [];
    private IEnumerable<ModelOptionsId> selectedModelIds = [];

    protected override async Task OnInitializedAsync()
    {
        var provider = await ProviderStore.GetAsync(Id);
        if (provider is null) { Nav.NavigateTo("/providers"); return; }

        form.Name = provider.Name;
        form.ApiUrl = provider.ApiUrl;
        form.ApiTokenSecretId = provider.ApiTokenSecretId;
        selectedModelIds = provider.Models.Select(m => m.Id).ToList();

        allModels = await ModelStore.GetAllAsync();
        allSecrets = await SecretStore.GetAllAsync();
    }

    private async Task HandleSubmit()
    {
        var provider = new ProviderOptions
        {
            Id = Id,
            Name = form.Name,
            ApiUrl = form.ApiUrl,
            ApiTokenSecretId = form.ApiTokenSecretId,
        };
        await ProviderStore.UpdateAsync(provider);
        await ProviderStore.AssignModelsAsync(Id, selectedModelIds.Select(id => id.Value));
        Nav.NavigateTo("/providers");
    }

    private sealed class FormModel
    {
        public string? Name { get; set; }
        public string? ApiUrl { get; set; }
        public SecretOptionsId? ApiTokenSecretId { get; set; }
    }
}
```

Also at the top of the file, add `@inject SecretStore SecretStore` and the `using` for `Legion.Admin.Data.Models` (likely already imported via _Imports.razor — verify by building).

> **Note for implementer:** Search for `SecretStore` to confirm it exists. `grep -rn "class SecretStore" src/libs/Legion.Admin.Data` — if missing, instead query `db.Secrets.AsNoTracking().ToListAsync()` via a scoped service, or extend the plan to add `SecretStore`. If `SecretStore` does not exist, create it as `public class SecretStore(AppDbContext db) { public Task<List<SecretOptions>> GetAllAsync(CancellationToken ct = default) => db.Secrets.AsNoTracking().OrderBy(s => s.Path).ToListAsync(ct); }` in `src/libs/Legion.Admin.Data/Stores/SecretStore.cs` and register it in `AgentDbContextExtensions.AddAgentStores()` next to the other stores.

- [ ] **Step 3: Apply the same change in `Providers.Create.razor`**

Replace the API-token markup row and `FormModel` identically. The `HandleSubmit` should set `ApiTokenSecretId = form.ApiTokenSecretId` and not pass any string.

- [ ] **Step 4: Build the solution**

Run: `dotnet build Legion.slnx --nologo`
Expected: Build succeeded with 0 errors. Pre-existing warnings are OK.

If `SecretStore` does not exist, create it now and register it as described in the Step 2 note.

- [ ] **Step 5: Commit**

```bash
git add src/libs/Legion.Admin.Data.Models src/libs/Legion.Admin.Data/Configurations/ProviderOptionsConfiguration.cs src/libs/Legion.Admin.Data/Stores src/libs/Legion.Admin.UI/Pages/Providers src/libs/Legion.Admin.Data/Seeds/ISeedEntity.cs src/libs/Legion.Admin.Data/Seeds/SeedEntityRegistry.cs src/libs/Legion.Admin.Data/Seeds/Dtos
git commit -m "feat(providers): replace ApiToken string with FK to SecretOptions"
```

---

## Task 4: Generate EF Core migrations for both providers

**Files:**
- Create: `src/libs/Legion.Admin.Data.Sqlite/Migrations/App/<timestamp>_AddProviderApiTokenSecretFk.{cs,Designer.cs}`
- Create: `src/libs/Legion.Admin.Data.PostgreSQL/Migrations/App/<timestamp>_AddProviderApiTokenSecretFk.{cs,Designer.cs}`
- Modify: `src/libs/Legion.Admin.Data.{Sqlite,PostgreSQL}/Migrations/App/AppDbContextModelSnapshot.cs` (auto-updated)

- [ ] **Step 1: Generate the SQLite migration**

```bash
cd src/libs/Legion.Admin.Data.Sqlite
./update-migrations.sh AddProviderApiTokenSecretFk
cd ../../..
```

Expected: New migration files appear under `Migrations/App/`. Auth migration may also be created with no changes — if so, delete the empty Auth migration files (`rm src/libs/Legion.Admin.Data.Sqlite/Migrations/Auth/<timestamp>_AddProviderApiTokenSecretFk*`).

- [ ] **Step 2: Generate the PostgreSQL migration**

```bash
cd src/libs/Legion.Admin.Data.PostgreSQL
./update-migrations.sh AddProviderApiTokenSecretFk
cd ../../..
```

Same — delete empty Auth-context migrations if generated.

- [ ] **Step 3: Inspect both migration `Up()` methods**

Each should `DropColumn(name: "ApiToken", table: "Providers", schema: "agents")` and `AddColumn<Guid>(name: "ApiTokenSecretId", ..., nullable: true)` plus `AddForeignKey(...)` to `Secrets`. Verify by running:

```bash
grep -A 30 "protected override void Up" src/libs/Legion.Admin.Data.Sqlite/Migrations/App/*_AddProviderApiTokenSecretFk.cs
grep -A 30 "protected override void Up" src/libs/Legion.Admin.Data.PostgreSQL/Migrations/App/*_AddProviderApiTokenSecretFk.cs
```

Expected: Both show the column drop, column add, index, and FK to Secrets table.

- [ ] **Step 4: Build the migration projects**

Run: `dotnet build src/libs/Legion.Admin.Data.Sqlite/Legion.Admin.Data.Sqlite.csproj src/libs/Legion.Admin.Data.PostgreSQL/Legion.Admin.Data.PostgreSQL.csproj --nologo`
Expected: Build succeeded.

- [ ] **Step 5: Delete dev databases so the new migrations run cleanly**

(Dev-only data per user instruction.) Search for and delete any local `.db` files:

```bash
find . -maxdepth 5 -name "agent.db*" -o -name "auth.db*" 2>/dev/null
# Confirm files, then:
find . -maxdepth 5 \( -name "agent.db*" -o -name "auth.db*" \) -delete
```

- [ ] **Step 6: Commit**

```bash
git add src/libs/Legion.Admin.Data.Sqlite/Migrations src/libs/Legion.Admin.Data.PostgreSQL/Migrations
git commit -m "feat(db): add migration for Provider.ApiTokenSecretId FK"
```

---

## Task 5: Refactor `SeedPayload`

**Files:**
- Modify: `src/libs/Legion.Admin.Data/Seeds/SeedPayload.cs`
- Delete: `src/libs/Legion.Admin.Data/Seeds/Dtos/SeedAgentDto.cs`

- [ ] **Step 1: Replace `SeedPayload`**

```csharp
// src/libs/Legion.Admin.Data/Seeds/SeedPayload.cs
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Legion.Admin.Data.Models.Providers;
using Legion.Admin.Data.Seeds.Dtos;

namespace Legion.Admin.Data.Seeds;

public class SeedPayload
{
    public List<SecretOptions> Secrets { get; } = [];
    public List<ProviderOptions> Providers { get; } = [];
    public List<AgentOptions> Agents { get; } = [];
    public List<SeedUserDto> Users { get; } = [];
    public List<OidcApplicationDto> OidcApplications { get; } = [];
    public List<OidcScopeDto> OidcScopes { get; } = [];
}
```

- [ ] **Step 2: Delete `SeedAgentDto.cs`**

```bash
git rm src/libs/Legion.Admin.Data/Seeds/Dtos/SeedAgentDto.cs
```

- [ ] **Step 3: Build (will fail — `YamlSeedLoader` and `AdminDbSeedService` reference old shape; we fix in Tasks 6-7)**

Don't commit yet — bundled with Task 7.

---

## Task 6: Rewrite `YamlSeedLoader` for type-discriminated `entities:` format

**Files:**
- Modify: `src/libs/Legion.Admin.Data/Seeds/YamlSeedLoader.cs`

- [ ] **Step 1: Rewrite the loader**

```csharp
// src/libs/Legion.Admin.Data/Seeds/YamlSeedLoader.cs
using System.Reflection;
using System.Text.RegularExpressions;
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Legion.Admin.Data.Models.Providers;
using Legion.Admin.Data.Seeds.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Legion.Admin.Data.Seeds;

public class YamlSeedLoader(IConfiguration configuration, ILogger<YamlSeedLoader> logger)
{
    private static readonly string[] SensitiveFields = ["password", "clientSecret", "encryptedValue"];

    private static readonly string[] KnownPermissionPrefixes =
        ["ept:", "gt:", "rt:", "scp:"];

    public SeedPayload LoadAll(string seedFolderPath)
    {
        var payload = new SeedPayload();

        if (!Directory.Exists(seedFolderPath))
        {
            logger.LogWarning("Seed folder not found at '{Path}' — skipping seed load", seedFolderPath);
            return payload;
        }

        var files = Directory
            .EnumerateFiles(seedFolderPath, "*.*", SearchOption.TopDirectoryOnly)
            .Where(f => f.EndsWith(".yml", StringComparison.OrdinalIgnoreCase)
                     || f.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            try
            {
                var yaml = File.ReadAllText(file);
                var interpolated = Interpolate(yaml);
                var doc = DeserializeDocument(interpolated);
                if (doc?.Entities is null) continue;

                foreach (var entity in doc.Entities)
                {
                    GuardSensitiveFields(file, entity);
                    Dispatch(payload, entity, file);
                }
            }
            catch (YamlException ex)
            {
                logger.LogError(ex, "Malformed YAML in '{File}' at line {Line} — skipping file",
                    Path.GetFileName(file), ex.Start.Line);
            }
        }

        return payload;
    }

    private static SeedDocument? DeserializeDocument(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithTypeDiscriminatingNodeDeserializer(o =>
            {
                o.AddKeyValueTypeDiscriminator<ISeedEntity>("seedType", SeedEntityRegistry.Map);
            })
            .Build();
        return deserializer.Deserialize<SeedDocument>(yaml);
    }

    private string Interpolate(string yaml) =>
        Regex.Replace(yaml, @"\$\{([^}]+)\}", match =>
        {
            var resolved = configuration[match.Groups[1].Value];
            // Preserve original placeholder if unresolved — caught later by GuardSensitiveFields.
            return resolved ?? match.Value;
        });

    private void GuardSensitiveFields(string fileName, ISeedEntity dto)
    {
        foreach (var field in SensitiveFields)
        {
            var prop = dto.GetType().GetProperty(field,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            var value = prop?.GetValue(dto) as string;
            if (value is null) continue;

            if (value.StartsWith("${"))
                throw new InvalidOperationException(
                    $"Seed file '{Path.GetFileName(fileName)}': '{field}' contains an unresolved placeholder '{value}'. " +
                    $"Set the config key via User Secrets or environment variables.");
        }
    }

    private void Dispatch(SeedPayload payload, ISeedEntity entity, string file)
    {
        switch (entity)
        {
            case SecretOptions s:
                if (payload.Secrets.Any(x => x.Path == s.Path))
                    LogDuplicate("secret", s.Path, file);
                else
                    payload.Secrets.Add(s);
                break;
            case ProviderOptions p:
                if (payload.Providers.Any(x => x.Name == p.Name))
                    LogDuplicate("provider", p.Name ?? "(null)", file);
                else
                    payload.Providers.Add(p);
                break;
            case AgentOptions a:
                if (payload.Agents.Any(x => x.Name == a.Name))
                    LogDuplicate("agent", a.Name ?? "(null)", file);
                else
                    payload.Agents.Add(a);
                break;
            case SeedUserDto u:
                if (payload.Users.Any(x => x.UserName == u.UserName))
                    LogDuplicate("user", u.UserName, file);
                else
                    payload.Users.Add(u);
                break;
            case OidcApplicationDto app:
                ValidatePermissions(app.Permissions, file);
                if (payload.OidcApplications.Any(x => x.ClientId == app.ClientId))
                    LogDuplicate("oidc-application", app.ClientId, file);
                else
                    payload.OidcApplications.Add(app);
                break;
            case OidcScopeDto sc:
                if (payload.OidcScopes.Any(x => x.Name == sc.Name))
                    LogDuplicate("oidc-scope", sc.Name, file);
                else
                    payload.OidcScopes.Add(sc);
                break;
            default:
                logger.LogWarning("Unhandled seed entity type {Type} in '{File}'",
                    entity.GetType().Name, Path.GetFileName(file));
                break;
        }
    }

    private void LogDuplicate(string kind, string key, string file) =>
        logger.LogWarning("Duplicate {Kind} '{Key}' in '{File}' — skipping",
            kind, key, Path.GetFileName(file));

    private void ValidatePermissions(List<string> permissions, string file)
    {
        foreach (var permission in permissions)
        {
            if (!KnownPermissionPrefixes.Any(p => permission.StartsWith(p, StringComparison.Ordinal)))
                logger.LogWarning(
                    "Unrecognised permission prefix in '{File}': '{Permission}'",
                    Path.GetFileName(file), permission);
        }
    }

    private sealed class SeedDocument
    {
        public List<ISeedEntity> Entities { get; set; } = [];
    }
}
```

> **Note:** Interpolation now operates on the raw YAML text *before* deserialization. This is simpler and avoids walking the deserialized graph. Sensitive-field guarding still runs after deserialization to catch fields that *would* have a placeholder.

- [ ] **Step 2: Build the data project**

Run: `dotnet build src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj --nologo`
Expected: Build succeeded.

> **If YamlDotNet's `WithTypeDiscriminatingNodeDeserializer` API differs from this signature** (it accepts an `Action<ITypeDiscriminatingNodeDeserializerOptions>`): consult `dotnet --list-sdks` and `find ~/.nuget/packages/yamldotnet -name "*.xml" | head` for IntelliSense docs, or read the YamlDotNet 16.3.0 source. The expected call is:
> ```csharp
> .WithTypeDiscriminatingNodeDeserializer(opts =>
>     opts.AddKeyValueTypeDiscriminator<ISeedEntity>("seedType", SeedEntityRegistry.Map))
> ```
> If this errors, fall back to manual dispatch: deserialize each `entities` item as `Dictionary<string, object>`, read `seedType`, then re-serialize and deserialize as the mapped concrete type. (See `DeserializeList<T>` pattern in the original loader for the round-trip approach.)

---

## Task 7: Refactor `AdminDbSeedService` for two-pass FK resolution

**Files:**
- Modify: `src/libs/Legion.Admin.Data/Services/AdminDbSeedService.cs`

- [ ] **Step 1: Replace the service**

```csharp
// src/libs/Legion.Admin.Data/Services/AdminDbSeedService.cs
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Legion.Admin.Data.Models.Providers;
using Legion.Admin.Data.Seeds;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Legion.Admin.Data.Services;

public class AdminDbSeedService(
    ILogger<AdminDbSeedService> logger,
    IServiceProvider serviceProvider,
    IWebHostEnvironment env,
    IConfiguration configuration,
    YamlSeedLoader loader) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (env.EnvironmentName != "Development") return;

        var seedPath = ResolveSeedPath();
        var payload = loader.LoadAll(seedPath);

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await SeedSecretsAsync(db, payload, cancellationToken);
        await SeedProvidersAsync(db, payload, cancellationToken);
        await SeedAgentsAsync(db, payload, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedSecretsAsync(AppDbContext db, SeedPayload payload, CancellationToken ct)
    {
        var existing = await db.Secrets.AsNoTracking()
            .Select(s => s.Path).ToListAsync(ct);
        var existingSet = existing.ToHashSet(StringComparer.Ordinal);

        foreach (var secret in payload.Secrets)
        {
            if (existingSet.Contains(secret.Path)) continue;
            var copy = secret with
            {
                Id = secret.Id == default ? SecretOptionsId.New() : secret.Id,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            db.Secrets.Add(copy);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedProvidersAsync(AppDbContext db, SeedPayload payload, CancellationToken ct)
    {
        // Resolve secret paths against persisted Secrets (single authoritative source).
        var secretsByPath = await db.Secrets.AsNoTracking()
            .ToDictionaryAsync(s => s.Path, s => s.Id, ct);

        foreach (var provider in payload.Providers)
        {
            if (!string.IsNullOrEmpty(provider.ApiTokenSecretPath))
            {
                if (!secretsByPath.TryGetValue(provider.ApiTokenSecretPath, out var secretId))
                    throw new InvalidOperationException(
                        $"Provider '{provider.Name}' references unknown secret path " +
                        $"'{provider.ApiTokenSecretPath}'. Define it in seed YAML before referencing.");
                provider.ApiTokenSecretId = secretId;
            }

            if (await db.Providers.AnyAsync(p => p.Name == provider.Name, ct)) continue;

            var copy = provider with
            {
                Id = provider.Id == default ? ProviderOptionsId.New() : provider.Id,
                ApiTokenSecretPath = null,  // not persisted, but blank it for clarity
                Models = [],
                Agents = [],
            };
            db.Providers.Add(copy);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedAgentsAsync(AppDbContext db, SeedPayload payload, CancellationToken ct)
    {
        var providersByName = await db.Providers.AsNoTracking()
            .Where(p => p.Name != null)
            .ToDictionaryAsync(p => p.Name!, p => p.Id, ct);

        foreach (var agent in payload.Agents)
        {
            if (!string.IsNullOrEmpty(agent.ProviderName))
            {
                if (!providersByName.TryGetValue(agent.ProviderName, out var providerId))
                    throw new InvalidOperationException(
                        $"Agent '{agent.Name}' references unknown provider " +
                        $"'{agent.ProviderName}'. Define it in seed YAML before referencing.");
                agent.ProviderId = providerId;
            }

            if (await db.Agents.AnyAsync(a => a.Name == agent.Name, ct)) continue;

            var copy = agent with
            {
                Id = agent.Id == default ? AgentOptionsId.New() : agent.Id,
                ProviderName = null,
                Models = [],
                Skills = [],
                Tools = [],
                McpServers = [],
                Middleware = [],
            };
            db.Agents.Add(copy);
        }
        await db.SaveChangesAsync(ct);
    }

    private string ResolveSeedPath()
    {
        var configured = configuration["Seeding:Path"] ?? "seed";
        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);
    }
}
```

- [ ] **Step 2: Build the solution**

Run: `dotnet build Legion.slnx --nologo`
Expected: Build succeeded.

- [ ] **Step 3: Commit Tasks 5–7**

```bash
git add src/libs/Legion.Admin.Data/Seeds src/libs/Legion.Admin.Data/Services/AdminDbSeedService.cs
git commit -m "refactor(seed): type-discriminated YAML + two-pass FK resolution"
```

---

## Task 8: Migrate seed YAML files to new format

**Files:**
- Modify: `src/WebDev/seed/agents.yml`
- Modify: `src/WebDev/seed/users.yml`
- Modify: `src/WebDev/seed/oidc-applications.yml`
- Modify: `src/WebDev/seed/oidc-scopes.yml`
- Create: `src/WebDev/seed/secrets.yml`
- Create: `src/WebDev/seed/providers.yml`

- [ ] **Step 1: Create `secrets.yml`**

```yaml
# src/WebDev/seed/secrets.yml
entities:
  - seedType: secret
    path: providers/anthropic/api-token
    description: Anthropic API token for the default provider
    encryptedValue: "${Seeding:AnthropicApiToken}"
```

- [ ] **Step 2: Create `providers.yml`**

```yaml
# src/WebDev/seed/providers.yml
entities:
  - seedType: provider
    name: Anthropic
    type: Anthropic
    apiUrl: https://api.anthropic.com
    apiTokenSecretPath: providers/anthropic/api-token
```

- [ ] **Step 3: Migrate `agents.yml`**

```yaml
# src/WebDev/seed/agents.yml
entities:
  - seedType: agent
    name: Default Agent
    description: The default Legion agent
    providerName: Anthropic
```

- [ ] **Step 4: Migrate `users.yml`**

```yaml
# src/WebDev/seed/users.yml
entities:
  - seedType: user
    userName: admin
    email: admin@legion.local
    emailConfirmed: true
    password: "${Seeding:AdminPassword}"
```

- [ ] **Step 5: Migrate `oidc-applications.yml`**

```yaml
# src/WebDev/seed/oidc-applications.yml
entities:
  - seedType: oidc-application
    clientId: legion-bff-client-id
    clientSecret: "${Seeding:BffClientSecret}"
    clientType: confidential
    consentType: implicit
    displayName: Legion BFF
    redirectUris:
      - "${OpenIddict:Authority}/signin-oidc"
    postLogoutRedirectUris:
      - "${OpenIddict:Authority}/signout-callback-oidc"
    permissions:
      - ept:authorization
      - ept:end_session
      - ept:token
      - gt:authorization_code
      - rt:code
      - scp:openid
      - scp:profile
      - scp:legion-api

  - seedType: oidc-application
    clientId: legion-api-client-id
    clientSecret: "${Seeding:ApiClientSecret}"
    clientType: confidential
    displayName: Legion API Test Client
    permissions:
      - ept:token
      - gt:client_credentials
      - scp:legion-api
```

- [ ] **Step 6: Migrate `oidc-scopes.yml`**

```yaml
# src/WebDev/seed/oidc-scopes.yml
entities:
  - seedType: oidc-scope
    name: legion-api
    resources:
      - legion-webhost
```

- [ ] **Step 7: Add a development default for `Seeding:AnthropicApiToken`**

In `src/WebDev/appsettings.Development.json`, ensure there's a placeholder. If the file doesn't already have a `Seeding` section, add:

```json
{
  "Seeding": {
    "AnthropicApiToken": "dev-only-placeholder-not-a-real-secret"
  }
}
```

(If the file is structured differently, merge `Seeding:AnthropicApiToken` into the existing config without disturbing other keys. This value will be read as a literal string and stored in `EncryptedValue` — in dev that's fine.)

- [ ] **Step 8: Commit**

```bash
git add src/WebDev/seed src/WebDev/appsettings.Development.json
git commit -m "feat(seed): migrate seed YAML to entities/seedType format and add provider+secret"
```

---

## Task 9: Update existing seed-loader tests + add new tests

**Files:**
- Modify: `tests/Legion.Admin.Data.Tests/Seeds/YamlSeedLoaderTests.cs`
- Modify: `tests/Legion.Admin.Data.Tests/Seeds/YamlSeedIntegrationTests.cs`
- Add new test methods covering provider+secret seeding and FK resolution.

- [ ] **Step 1: Replace `YamlSeedLoaderTests.cs`**

Read the current file first to preserve any helpers, then replace its contents with tests targeting the new `entities:` format. Required test cases:

```csharp
using Legion.Admin.Data.Models.Providers;
using Legion.Admin.Data.Seeds;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legion.Admin.Data.Tests.Seeds;

public class YamlSeedLoaderTests
{
    private static YamlSeedLoader BuildLoader(Dictionary<string, string?>? config = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config ?? [])
            .Build();
        return new YamlSeedLoader(configuration, NullLogger<YamlSeedLoader>.Instance);
    }

    private static string WriteTempYaml(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "seed.yml"), content);
        return dir;
    }

    [Fact]
    public void LoadAll_MissingFolder_ReturnsEmptyPayload()
    {
        var payload = BuildLoader().LoadAll("/nonexistent/path");
        Assert.Empty(payload.Secrets);
        Assert.Empty(payload.Providers);
        Assert.Empty(payload.Agents);
        Assert.Empty(payload.Users);
    }

    [Fact]
    public void LoadAll_AgentEntity_ParsesAgent()
    {
        var dir = WriteTempYaml("""
            entities:
              - seedType: agent
                name: My Agent
                description: A test agent
            """);

        var payload = BuildLoader().LoadAll(dir);

        Assert.Single(payload.Agents);
        Assert.Equal("My Agent", payload.Agents[0].Name);
        Assert.Equal("A test agent", payload.Agents[0].Description);
    }

    [Fact]
    public void LoadAll_SecretEntity_ParsesSecret()
    {
        var dir = WriteTempYaml("""
            entities:
              - seedType: secret
                path: providers/test/key
                description: Test secret
                encryptedValue: literal-value
            """);

        var payload = BuildLoader().LoadAll(dir);

        Assert.Single(payload.Secrets);
        Assert.Equal("providers/test/key", payload.Secrets[0].Path);
        Assert.Equal("literal-value", payload.Secrets[0].EncryptedValue);
    }

    [Fact]
    public void LoadAll_ProviderEntity_PopulatesApiTokenSecretPath()
    {
        var dir = WriteTempYaml("""
            entities:
              - seedType: provider
                name: Anthropic
                type: Anthropic
                apiUrl: https://api.anthropic.com
                apiTokenSecretPath: providers/anthropic/api-token
            """);

        var payload = BuildLoader().LoadAll(dir);

        var p = Assert.Single(payload.Providers);
        Assert.Equal("Anthropic", p.Name);
        Assert.Equal(ProviderType.Anthropic, p.Type);
        Assert.Equal("providers/anthropic/api-token", p.ApiTokenSecretPath);
        Assert.Null(p.ApiTokenSecretId);
    }

    [Fact]
    public void LoadAll_InterpolatesConfigPlaceholder()
    {
        var dir = WriteTempYaml("""
            entities:
              - seedType: oidc-scope
                name: ${MyConfig:ScopeName}
                resources: []
            """);

        var payload = BuildLoader(new Dictionary<string, string?>
        {
            ["MyConfig:ScopeName"] = "my-scope"
        }).LoadAll(dir);

        Assert.Single(payload.OidcScopes);
        Assert.Equal("my-scope", payload.OidcScopes[0].Name);
    }

    [Fact]
    public void LoadAll_UnresolvedPlaceholderInSensitiveField_Throws()
    {
        var dir = WriteTempYaml("""
            entities:
              - seedType: user
                userName: admin
                email: admin@legion.local
                emailConfirmed: true
                password: "${Seeding:Missing}"
            """);

        var ex = Assert.Throws<InvalidOperationException>(() => BuildLoader().LoadAll(dir));
        Assert.Contains("password", ex.Message);
        Assert.Contains("unresolved placeholder", ex.Message);
    }

    [Fact]
    public void LoadAll_UnresolvedPlaceholderInSecretEncryptedValue_Throws()
    {
        var dir = WriteTempYaml("""
            entities:
              - seedType: secret
                path: providers/x/key
                encryptedValue: "${Seeding:Missing}"
            """);

        var ex = Assert.Throws<InvalidOperationException>(() => BuildLoader().LoadAll(dir));
        Assert.Contains("encryptedValue", ex.Message);
    }

    [Fact]
    public void LoadAll_DuplicateProviderName_LogsAndSkips()
    {
        var dir = WriteTempYaml("""
            entities:
              - seedType: provider
                name: Anthropic
                type: Anthropic
              - seedType: provider
                name: Anthropic
                type: Anthropic
            """);

        var payload = BuildLoader().LoadAll(dir);
        Assert.Single(payload.Providers);
    }

    [Fact]
    public void LoadAll_MultipleEntityTypesInOneFile_AllParsed()
    {
        var dir = WriteTempYaml("""
            entities:
              - seedType: secret
                path: providers/x/key
                encryptedValue: v1
              - seedType: provider
                name: X
                type: Custom
                apiTokenSecretPath: providers/x/key
              - seedType: agent
                name: A
                providerName: X
            """);

        var payload = BuildLoader().LoadAll(dir);
        Assert.Single(payload.Secrets);
        Assert.Single(payload.Providers);
        Assert.Single(payload.Agents);
    }
}
```

- [ ] **Step 2: Update `YamlSeedIntegrationTests.cs`**

Read the current file (`cat tests/Legion.Admin.Data.Tests/Seeds/YamlSeedIntegrationTests.cs`) to understand what it asserts. Update each YAML literal to the new `entities:` format. Keep the same assertions about parsing outcomes — only the input shape changes. If the file tests reading the actual `src/WebDev/seed/*.yml` files, the new format is already what those files contain after Task 8.

- [ ] **Step 3: Run the seed tests**

Run: `dotnet test tests/Legion.Admin.Data.Tests/Legion.Admin.Data.Tests.csproj --filter "FullyQualifiedName~Seeds" --nologo`
Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add tests/Legion.Admin.Data.Tests/Seeds
git commit -m "test(seed): cover entities/seedType YAML format and FK linkage"
```

---

## Task 10: Add seed-service integration tests for FK resolution

**Files:**
- Create: `tests/Legion.Admin.Data.Tests/Seeds/AdminDbSeedServiceTests.cs`

- [ ] **Step 1: Write integration tests using in-memory provider**

Check if the test project already references `Microsoft.EntityFrameworkCore.InMemory` or `Microsoft.EntityFrameworkCore.Sqlite`:

```bash
grep -E "InMemory|Sqlite" tests/Legion.Admin.Data.Tests/Legion.Admin.Data.Tests.csproj
```

If `Sqlite` is referenced (more realistic since the app uses Sqlite), use it. Otherwise use InMemory. Pattern:

```csharp
using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Providers;
using Legion.Admin.Data.Seeds;
using Legion.Admin.Data.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legion.Admin.Data.Tests.Seeds;

public class AdminDbSeedServiceTests
{
    private sealed class FakeEnv : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
    }

    private static (IServiceProvider sp, AppDbContext db, FakeEnv env) Build(string seedDir)
    {
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o =>
            o.UseSqlite($"DataSource=file:test_{Guid.NewGuid():N}?mode=memory&cache=shared"));
        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        return (sp, db, new FakeEnv { ContentRootPath = seedDir });
    }

    private static string WriteSeed(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(dir, "seed"));
        File.WriteAllText(Path.Combine(dir, "seed", "all.yml"), content);
        return dir;
    }

    [Fact]
    public async Task StartAsync_LinksProviderToSecretByPath()
    {
        var contentRoot = WriteSeed("""
            entities:
              - seedType: secret
                path: providers/x/key
                encryptedValue: literal-value
              - seedType: provider
                name: X
                type: Custom
                apiTokenSecretPath: providers/x/key
            """);
        var (sp, db, env) = Build(contentRoot);

        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var loader = new YamlSeedLoader(config, NullLogger<YamlSeedLoader>.Instance);
        var service = new AdminDbSeedService(NullLogger<AdminDbSeedService>.Instance, sp, env, config, loader);

        await service.StartAsync(CancellationToken.None);

        var provider = await db.Providers.AsNoTracking().Include(p => p.ApiTokenSecret)
            .FirstAsync(p => p.Name == "X");
        Assert.NotNull(provider.ApiTokenSecretId);
        Assert.NotNull(provider.ApiTokenSecret);
        Assert.Equal("providers/x/key", provider.ApiTokenSecret!.Path);
        Assert.Equal("literal-value", provider.ApiTokenSecret.EncryptedValue);
    }

    [Fact]
    public async Task StartAsync_DanglingSecretPath_Throws()
    {
        var contentRoot = WriteSeed("""
            entities:
              - seedType: provider
                name: X
                type: Custom
                apiTokenSecretPath: providers/missing/key
            """);
        var (sp, db, env) = Build(contentRoot);

        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var loader = new YamlSeedLoader(config, NullLogger<YamlSeedLoader>.Instance);
        var service = new AdminDbSeedService(NullLogger<AdminDbSeedService>.Instance, sp, env, config, loader);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));
        Assert.Contains("providers/missing/key", ex.Message);
    }

    [Fact]
    public async Task StartAsync_LinksAgentToProviderByName()
    {
        var contentRoot = WriteSeed("""
            entities:
              - seedType: provider
                name: X
                type: Custom
              - seedType: agent
                name: A
                providerName: X
            """);
        var (sp, db, env) = Build(contentRoot);

        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var loader = new YamlSeedLoader(config, NullLogger<YamlSeedLoader>.Instance);
        var service = new AdminDbSeedService(NullLogger<AdminDbSeedService>.Instance, sp, env, config, loader);

        await service.StartAsync(CancellationToken.None);

        var agent = await db.Agents.AsNoTracking().FirstAsync(a => a.Name == "A");
        var provider = await db.Providers.AsNoTracking().FirstAsync(p => p.Name == "X");
        Assert.Equal(provider.Id, agent.ProviderId);
    }
}
```

> **Note:** If the test project uses a different DbContext registration helper (`AddSqliteAppDbContext`, `AddInMemoryAgentDbContext`), prefer that to keep configuration consistent with WebDev.

- [ ] **Step 2: Run the new tests**

Run: `dotnet test tests/Legion.Admin.Data.Tests/Legion.Admin.Data.Tests.csproj --filter "FullyQualifiedName~AdminDbSeedServiceTests" --nologo`
Expected: All three tests pass.

- [ ] **Step 3: Commit**

```bash
git add tests/Legion.Admin.Data.Tests/Seeds/AdminDbSeedServiceTests.cs
git commit -m "test(seed): integration tests for FK resolution and dangling-ref errors"
```

---

## Task 11: Final solution build, full test run, and WebDev smoke

**Files:** none

- [ ] **Step 1: Run the full solution build**

Run: `dotnet build Legion.slnx --nologo 2>&1 | tail -20`
Expected: 0 errors. Pre-existing warnings (CS9113 unused `logger`, CS8602 in `Secrets.razor:126`) are acceptable.

- [ ] **Step 2: Run all Admin.Data + Secrets tests**

Run: `dotnet test tests/Legion.Admin.Data.Tests/Legion.Admin.Data.Tests.csproj tests/Legion.Secrets.Tests/Legion.Secrets.Tests.csproj --nologo 2>&1 | tail -10`
Expected: All tests pass.

- [ ] **Step 3: Smoke-test WebDev startup**

Run in background, capture logs, give it 30s, then kill:

```bash
cd src/WebDev
ASPNETCORE_ENVIRONMENT=Development dotnet run --no-build > /tmp/webdev.log 2>&1 &
WEBDEV_PID=$!
sleep 25
kill $WEBDEV_PID 2>/dev/null
cd ../..
grep -E "Now listening|error|Error|exception|Exception" /tmp/webdev.log | head -20
```

Expected: `Now listening on:` line appears; no fatal exceptions related to seeding. The `Default Agent` and `Anthropic` provider should be present in the database — verify with:

```bash
sqlite3 src/WebDev/agent.db "SELECT Name, ApiTokenSecretId FROM agents.Providers" 2>/dev/null \
 || echo "(sqlite3 not installed; skip — provider seeding success implied by clean startup)"
```

- [ ] **Step 4: Final commit if needed (verification only — no source changes expected)**

If smoke testing reveals issues, fix and commit individually. If clean, no commit needed.

---

## Acceptance / Success Criteria

- [ ] `ProviderOptions.ApiToken` (string) replaced with `ApiTokenSecretId` + `ApiTokenSecret` navigation in the model and database schema.
- [ ] EF Core migration generated for both Sqlite and PostgreSQL providers; both build successfully.
- [ ] `Provider.ApiTokenSecretPath` and `AgentOptions.ProviderName` are `[NotMapped]` and not in the DB schema.
- [ ] `YamlSeedLoader` uses `entities:` + `seedType` discriminator; no per-key switch dispatch in the load path.
- [ ] Adding a new seedable type requires exactly one line in `SeedEntityRegistry.Map`.
- [ ] `AdminDbSeedService` performs two-pass loading: secrets persist first, then provider/agent FKs resolve, then those persist.
- [ ] Strict FK mode: dangling `apiTokenSecretPath` or `providerName` throws `InvalidOperationException` with a clear message.
- [ ] All existing seed YAML files (`agents.yml`, `users.yml`, `oidc-applications.yml`, `oidc-scopes.yml`) are migrated to the new format.
- [ ] New `secrets.yml` and `providers.yml` exist and seed the Anthropic provider linked to its API-token secret.
- [ ] Provider Create/Update Razor pages use a `SecretOptions` dropdown (by `Path`) instead of an inline string field.
- [ ] All tests in `Legion.Admin.Data.Tests` and `Legion.Secrets.Tests` pass.
- [ ] WebDev starts cleanly in Development mode with the new seed system (no fatal seeding exceptions).
