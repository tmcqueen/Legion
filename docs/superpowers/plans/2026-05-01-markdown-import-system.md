# Markdown Import System — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a versioned prompt/skill/tool library with draft-publish workflow, agent prompt selection, and secure URL import — backed by three new EF Core tables and exposed through Radzen.Blazor admin pages.

**Architecture:** Three new EF entities (`PromptDefinition`, `PromptVersion`, `AgentPromptAssignment`) live in `Legion.Admin.Data.Models`, a new `IPromptStore` interface + `PromptStore` concrete class handles all persistence, `AgentFactory` queries assigned prompts on agent creation, and three API controllers + three Razor components expose the feature in WebDev.

**Tech Stack:** .NET 10, EF Core (SQLite + PostgreSQL), xunit + NSubstitute + in-memory EF (tests), Radzen.Blazor (UI), YamlDotNet (YAML validation — already referenced by Legion.Admin.Data).

---

## File Map

**New files:**
- `src/libs/Legion.Admin.Data.Models/Ids.cs` — *(modify)* add 3 new ID types
- `src/libs/Legion.Admin.Data.Models/Prompts/PromptType.cs` — enum
- `src/libs/Legion.Admin.Data.Models/Prompts/PromptCategory.cs` — enum
- `src/libs/Legion.Admin.Data.Models/Prompts/PromptStatus.cs` — enum
- `src/libs/Legion.Admin.Data.Models/Prompts/PromptDefinition.cs` — model
- `src/libs/Legion.Admin.Data.Models/Prompts/PromptVersion.cs` — model
- `src/libs/Legion.Admin.Data.Models/Prompts/AgentPromptAssignment.cs` — model
- `src/libs/Legion.Admin.Data/Configurations/PromptDefinitionConfiguration.cs` — EF config
- `src/libs/Legion.Admin.Data/Configurations/PromptVersionConfiguration.cs` — EF config
- `src/libs/Legion.Admin.Data/Configurations/AgentPromptAssignmentConfiguration.cs` — EF config
- `src/libs/Legion.Admin.Data/AppDbContext.cs` — *(modify)* 3 new DbSets
- `src/libs/Legion.Admin.Data/Stores/IPromptStore.cs` — interface
- `src/libs/Legion.Admin.Data/Stores/PromptStore.cs` — implementation
- `src/libs/Legion.Admin.Data.Sqlite/Extensions/SqliteExtensions.cs` — *(modify)* register IPromptStore
- `src/libs/Legion.Admin.Data.PostgreSQL/Extensions/PostgreSqlExtensions.cs` — *(modify)* register IPromptStore
- `src/Legion.Agents/Providers/AgentOptions.cs` — *(modify)* add ToolWhitelist/ToolBlacklist
- `src/Legion.Agents/Providers/AgentFactory.cs` — *(modify)* prompt assembly + tool filtering
- `src/WebDev/Controllers/PromptsController.cs` — CRUD for definitions/versions
- `src/WebDev/Controllers/ImportController.cs` — URL import with SSRF guard
- `src/WebDev/Controllers/AgentPromptsController.cs` — prompt assignments per agent
- `src/WebDev/Components/Pages/Prompts.razor` — `/admin/prompts` library page
- `src/WebDev/Components/Shared/PromptEditor.razor` — reusable markdown+frontmatter editor
- `src/WebDev/Components/Shared/PromptDetailPanel.razor` — detail + draft editor panel (right pane)
- `src/WebDev/Components/Shared/ImportPromptDialog.razor` — import dialog (URL + paste)
- `src/WebDev/Components/Shared/PromptSelector.razor` — agent configuration modal
- `tests/Legion.Admin.Data.Tests/Prompts/PromptStoreTests.cs` — store unit tests
- `tests/Legion.Admin.Data.Tests/Prompts/AgentFactoryPromptTests.cs` — factory unit tests

**Migrations (auto-generated — run commands in Task 6):**
- `src/libs/Legion.Admin.Data.Sqlite/Migrations/App/<timestamp>_AddPromptLibrary.cs`
- `src/libs/Legion.Admin.Data.PostgreSQL/Migrations/App/<timestamp>_AddPromptLibrary.cs`

---

## Task 1: Branded ID Types

**Files:**
- Modify: `src/libs/Legion.Admin.Data.Models/Ids.cs`

- [ ] **Step 1: Add 3 ID types to Ids.cs**

  Append to the bottom of `src/libs/Legion.Admin.Data.Models/Ids.cs`:

  ```csharp
  public readonly record struct PromptDefinitionId(Guid Value)
  {
      public static PromptDefinitionId New() => new(Guid.CreateVersion7());
      public static implicit operator Guid(PromptDefinitionId id) => id.Value;
      public static implicit operator PromptDefinitionId(Guid value) => new(value);
      public override string ToString() => Value.ToString();
  }

  public readonly record struct PromptVersionId(Guid Value)
  {
      public static PromptVersionId New() => new(Guid.CreateVersion7());
      public static implicit operator Guid(PromptVersionId id) => id.Value;
      public static implicit operator PromptVersionId(Guid value) => new(value);
      public override string ToString() => Value.ToString();
  }

  public readonly record struct AgentPromptAssignmentId(Guid Value)
  {
      public static AgentPromptAssignmentId New() => new(Guid.CreateVersion7());
      public static implicit operator Guid(AgentPromptAssignmentId id) => id.Value;
      public static implicit operator AgentPromptAssignmentId(Guid value) => new(value);
      public override string ToString() => Value.ToString();
  }
  ```

- [ ] **Step 2: Build to confirm no errors**

  ```bash
  dotnet build src/libs/Legion.Admin.Data.Models/Legion.Admin.Data.Models.csproj
  ```
  Expected: Build succeeded.

- [ ] **Step 3: Commit**

  ```bash
  git add src/libs/Legion.Admin.Data.Models/Ids.cs
  git commit -m "feat: add PromptDefinitionId, PromptVersionId, AgentPromptAssignmentId branded ID types"
  ```

---

## Task 2: Domain Models + Enums

**Files:**
- Create: `src/libs/Legion.Admin.Data.Models/Prompts/PromptType.cs`
- Create: `src/libs/Legion.Admin.Data.Models/Prompts/PromptCategory.cs`
- Create: `src/libs/Legion.Admin.Data.Models/Prompts/PromptStatus.cs`
- Create: `src/libs/Legion.Admin.Data.Models/Prompts/PromptDefinition.cs`
- Create: `src/libs/Legion.Admin.Data.Models/Prompts/PromptVersion.cs`
- Create: `src/libs/Legion.Admin.Data.Models/Prompts/AgentPromptAssignment.cs`

- [ ] **Step 1: Create enums**

  Create `src/libs/Legion.Admin.Data.Models/Prompts/PromptType.cs`:
  ```csharp
  namespace Legion.Admin.Data.Models.Prompts;

  public enum PromptType { Prompt, Skill, ToolDescription }
  ```

  Create `src/libs/Legion.Admin.Data.Models/Prompts/PromptCategory.cs`:
  ```csharp
  namespace Legion.Admin.Data.Models.Prompts;

  public enum PromptCategory { Foundation = 0, Constraints = 1, TaskSpecific = 2, Overrides = 3 }
  ```

  Create `src/libs/Legion.Admin.Data.Models/Prompts/PromptStatus.cs`:
  ```csharp
  namespace Legion.Admin.Data.Models.Prompts;

  public enum PromptStatus { Draft, Published, Archived }
  ```

- [ ] **Step 2: Create PromptDefinition model**

  Create `src/libs/Legion.Admin.Data.Models/Prompts/PromptDefinition.cs`:
  ```csharp
  namespace Legion.Admin.Data.Models.Prompts;

  public class PromptDefinition
  {
      public PromptDefinitionId Id { get; init; } = PromptDefinitionId.New();
      public string Path { get; set; } = string.Empty;
      public PromptType Type { get; set; }
      public PromptCategory Category { get; set; }
      public bool IsDefaultIncluded { get; set; }
      public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
      public DateTime? DeletedAt { get; set; }

      public ICollection<PromptVersion> Versions { get; init; } = [];
      public ICollection<AgentPromptAssignment> Assignments { get; init; } = [];
  }
  ```

- [ ] **Step 3: Create PromptVersion model**

  Create `src/libs/Legion.Admin.Data.Models/Prompts/PromptVersion.cs`:
  ```csharp
  namespace Legion.Admin.Data.Models.Prompts;

  public class PromptVersion
  {
      public PromptVersionId Id { get; init; } = PromptVersionId.New();
      public PromptDefinitionId DefinitionId { get; init; }
      public PromptStatus Status { get; set; }
      public string Content { get; set; } = string.Empty;
      public string? Frontmatter { get; set; }
      public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
      public string CreatedBy { get; init; } = string.Empty;
      public string? Notes { get; set; }

      public PromptDefinition? Definition { get; init; }
  }
  ```

- [ ] **Step 4: Create AgentPromptAssignment model**

  Create `src/libs/Legion.Admin.Data.Models/Prompts/AgentPromptAssignment.cs`:
  ```csharp
  using Legion.Admin.Data.Models.Agents;

  namespace Legion.Admin.Data.Models.Prompts;

  public class AgentPromptAssignment
  {
      public AgentPromptAssignmentId Id { get; init; } = AgentPromptAssignmentId.New();
      public AgentOptionsId AgentId { get; init; }
      public PromptDefinitionId DefinitionId { get; init; }
      public int Order { get; set; }

      public PromptDefinition? Definition { get; init; }
  }
  ```

  > **Note:** `AgentOptionsId` is the concrete FK type for agents in this codebase. The spec calls this `AgentId` as a placeholder.

- [ ] **Step 5: Build to confirm no errors**

  ```bash
  dotnet build src/libs/Legion.Admin.Data.Models/Legion.Admin.Data.Models.csproj
  ```
  Expected: Build succeeded.

- [ ] **Step 6: Commit**

  ```bash
  git add src/libs/Legion.Admin.Data.Models/Prompts/
  git commit -m "feat: add PromptDefinition, PromptVersion, AgentPromptAssignment domain models"
  ```

---

## Task 3: EF Core Configurations

**Files:**
- Create: `src/libs/Legion.Admin.Data/Configurations/PromptDefinitionConfiguration.cs`
- Create: `src/libs/Legion.Admin.Data/Configurations/PromptVersionConfiguration.cs`
- Create: `src/libs/Legion.Admin.Data/Configurations/AgentPromptAssignmentConfiguration.cs`

- [ ] **Step 1: Create PromptDefinitionConfiguration**

  Create `src/libs/Legion.Admin.Data/Configurations/PromptDefinitionConfiguration.cs`:
  ```csharp
  using Legion.Admin.Data.Models;
  using Legion.Admin.Data.Models.Prompts;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Metadata.Builders;

  namespace Legion.Admin.Data.Configurations;

  public class PromptDefinitionConfiguration : IEntityTypeConfiguration<PromptDefinition>
  {
      public void Configure(EntityTypeBuilder<PromptDefinition> builder)
      {
          builder.Property(d => d.Id)
              .HasConversion(id => id.Value, value => new PromptDefinitionId(value));

          builder.Property(d => d.Path)
              .HasMaxLength(500)
              .IsRequired();

          builder.Property(d => d.Type)
              .HasConversion<string>()
              .HasMaxLength(50)
              .IsRequired();

          builder.Property(d => d.Category)
              .HasConversion<string>()
              .HasMaxLength(50)
              .IsRequired();

          builder.Property(d => d.IsDefaultIncluded)
              .HasDefaultValue(false);

          builder.HasIndex(d => d.Path)
              .IsUnique()
              .HasDatabaseName("ix_prompt_definitions_path");

          builder.HasMany(d => d.Versions)
              .WithOne(v => v.Definition)
              .HasForeignKey(v => v.DefinitionId)
              .OnDelete(DeleteBehavior.Cascade);

          builder.HasMany(d => d.Assignments)
              .WithOne(a => a.Definition)
              .HasForeignKey(a => a.DefinitionId)
              .OnDelete(DeleteBehavior.Cascade);
      }
  }
  ```

- [ ] **Step 2: Create PromptVersionConfiguration**

  Create `src/libs/Legion.Admin.Data/Configurations/PromptVersionConfiguration.cs`:
  ```csharp
  using Legion.Admin.Data.Models;
  using Legion.Admin.Data.Models.Prompts;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Metadata.Builders;

  namespace Legion.Admin.Data.Configurations;

  public class PromptVersionConfiguration : IEntityTypeConfiguration<PromptVersion>
  {
      public void Configure(EntityTypeBuilder<PromptVersion> builder)
      {
          builder.Property(v => v.Id)
              .HasConversion(id => id.Value, value => new PromptVersionId(value));

          builder.Property(v => v.DefinitionId)
              .HasConversion(id => id.Value, value => new PromptDefinitionId(value));

          builder.Property(v => v.Status)
              .HasConversion<string>()
              .HasMaxLength(20)
              .IsRequired();

          builder.Property(v => v.Content)
              .IsRequired();

          builder.Property(v => v.CreatedBy)
              .HasMaxLength(512)
              .IsRequired();

          // Filtered unique index: only one Published version per definition.
          // EF Core generates this; the migration will include a HasFilter clause.
          builder.HasIndex(v => v.DefinitionId)
              .HasFilter("\"Status\" = 'Published'")
              .IsUnique()
              .HasDatabaseName("ix_prompt_versions_definition_published");
      }
  }
  ```

- [ ] **Step 3: Create AgentPromptAssignmentConfiguration**

  Create `src/libs/Legion.Admin.Data/Configurations/AgentPromptAssignmentConfiguration.cs`:
  ```csharp
  using Legion.Admin.Data.Models;
  using Legion.Admin.Data.Models.Prompts;
  using Microsoft.EntityFrameworkCore;
  using Microsoft.EntityFrameworkCore.Metadata.Builders;

  namespace Legion.Admin.Data.Configurations;

  public class AgentPromptAssignmentConfiguration : IEntityTypeConfiguration<AgentPromptAssignment>
  {
      public void Configure(EntityTypeBuilder<AgentPromptAssignment> builder)
      {
          builder.Property(a => a.Id)
              .HasConversion(id => id.Value, value => new AgentPromptAssignmentId(value));

          builder.Property(a => a.AgentId)
              .HasConversion(id => id.Value, value => new AgentOptionsId(value));

          builder.Property(a => a.DefinitionId)
              .HasConversion(id => id.Value, value => new PromptDefinitionId(value));

          builder.Property(a => a.Order)
              .HasDefaultValue(0);

          builder.HasIndex(a => new { a.AgentId, a.DefinitionId })
              .IsUnique()
              .HasDatabaseName("ix_agent_prompt_assignments_agent_definition");
      }
  }
  ```

- [ ] **Step 4: Build to confirm no errors**

  ```bash
  dotnet build src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj
  ```
  Expected: Build succeeded.

- [ ] **Step 5: Commit**

  ```bash
  git add src/libs/Legion.Admin.Data/Configurations/PromptDefinitionConfiguration.cs \
          src/libs/Legion.Admin.Data/Configurations/PromptVersionConfiguration.cs \
          src/libs/Legion.Admin.Data/Configurations/AgentPromptAssignmentConfiguration.cs
  git commit -m "feat: add EF Core configurations for prompt library entities"
  ```

---

## Task 4: Update AppDbContext

**Files:**
- Modify: `src/libs/Legion.Admin.Data/AppDbContext.cs`

- [ ] **Step 1: Add 3 new DbSets**

  In `src/libs/Legion.Admin.Data/AppDbContext.cs`, add these 3 properties after the existing `DbSet` declarations and add the `Prompts` using:

  ```csharp
  using Legion.Admin.Data.Models;
  using Legion.Admin.Data.Models.Agents;
  using Legion.Admin.Data.Models.Prompts;
  using Legion.Admin.Data.Models.Providers;
  using Microsoft.EntityFrameworkCore;

  namespace Legion.Admin.Data;

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
      public DbSet<PromptDefinition> PromptDefinitions => Set<PromptDefinition>();
      public DbSet<PromptVersion> PromptVersions => Set<PromptVersion>();
      public DbSet<AgentPromptAssignment> AgentPromptAssignments => Set<AgentPromptAssignment>();

      protected override void OnModelCreating(ModelBuilder builder)
      {
          base.OnModelCreating(builder);
          builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
      }
  }
  ```

- [ ] **Step 2: Build to confirm no errors**

  ```bash
  dotnet build src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj
  ```
  Expected: Build succeeded.

- [ ] **Step 3: Commit**

  ```bash
  git add src/libs/Legion.Admin.Data/AppDbContext.cs
  git commit -m "feat: register PromptDefinition, PromptVersion, AgentPromptAssignment in AppDbContext"
  ```

---

## Task 5: Database Migrations

**Files:**
- Generate: `src/libs/Legion.Admin.Data.Sqlite/Migrations/App/<timestamp>_AddPromptLibrary.cs`
- Generate: `src/libs/Legion.Admin.Data.PostgreSQL/Migrations/App/<timestamp>_AddPromptLibrary.cs`

> **Context:** Migrations are auto-generated by EF Core tooling. Each provider project (SQLite, PostgreSQL) holds its own migration files. After generating, you must manually add the raw SQL for the filtered unique index because EF Core may not generate the `WHERE` clause automatically for SQLite.

- [ ] **Step 1: Generate SQLite migration**

  ```bash
  cd src/libs/Legion.Admin.Data.Sqlite
  dotnet ef migrations add AddPromptLibrary --context AppDbContext --project . --startup-project ../../WebDev -- --environment Development
  ```
  Expected: `Done. To undo this action, use 'ef migrations remove'`

- [ ] **Step 2: Verify and patch the SQLite migration**

  Open the generated `Migrations/App/<timestamp>_AddPromptLibrary.cs`. Verify it created:
  - `PromptDefinitions` table with `Path`, `Type`, `Category`, `IsDefaultIncluded`, `CreatedAt`, `DeletedAt`
  - `PromptVersions` table with FK to `PromptDefinitions`, `Status`, `Content`, `Frontmatter`, `CreatedAt`, `CreatedBy`, `Notes`
  - `AgentPromptAssignments` table with FK to `PromptDefinitions`, `AgentId`, `DefinitionId`, `Order`

  Find the `CreateIndex` for `ix_prompt_versions_definition_published`. If it was generated without a `filter:` argument, replace the generated `CreateIndex` call with:

  ```csharp
  migrationBuilder.CreateIndex(
      name: "ix_prompt_versions_definition_published",
      table: "PromptVersions",
      column: "DefinitionId",
      unique: true,
      filter: "\"Status\" = 'Published'");
  ```

  And in `Down()`, ensure `DropIndex` precedes `DropTable("PromptVersions")`.

- [ ] **Step 3: Apply the SQLite migration (verify it runs)**

  ```bash
  cd src/WebDev
  dotnet ef database update --context AppDbContext
  ```
  Expected: `Done.`

- [ ] **Step 4: Generate PostgreSQL migration**

  ```bash
  cd src/libs/Legion.Admin.Data.PostgreSQL
  dotnet ef migrations add AddPromptLibrary --context AppDbContext --project . --startup-project ../../WebDev -- --environment Development
  ```
  Expected: `Done. To undo this action, use 'ef migrations remove'`

  Verify and patch the same filtered index if needed:
  ```csharp
  migrationBuilder.CreateIndex(
      name: "ix_prompt_versions_definition_published",
      table: "PromptVersions",
      column: "DefinitionId",
      unique: true,
      filter: "\"Status\" = 'Published'");
  ```

- [ ] **Step 5: Commit**

  ```bash
  git add src/libs/Legion.Admin.Data.Sqlite/Migrations/ \
          src/libs/Legion.Admin.Data.PostgreSQL/Migrations/
  git commit -m "feat: add EF Core migrations for prompt library tables"
  ```

---

## Task 6: IPromptStore Interface + PromptStore Implementation

**Files:**
- Create: `src/libs/Legion.Admin.Data/Stores/IPromptStore.cs`
- Create: `src/libs/Legion.Admin.Data/Stores/PromptStore.cs`
- Modify: `src/libs/Legion.Admin.Data/Extensions/SqliteExtensions.cs` (in `Legion.Admin.Data.Sqlite` project)
- Modify: `src/libs/Legion.Admin.Data.PostgreSQL/Extensions/PostgreSqlExtensions.cs`

> **Note:** `PromptStore` is provider-neutral (uses EF Core only, no raw SQL). It lives in the base `Legion.Admin.Data` project. The `PublishDraftAsync` operation wraps its work in a transaction to prevent concurrent publish races.

- [ ] **Step 1: Write the failing test first** (see Task 7 — write tests before implementation)

  Skip ahead to Task 7 Step 1, then return here after tests are written.

- [ ] **Step 2: Create IPromptStore interface**

  Create `src/libs/Legion.Admin.Data/Stores/IPromptStore.cs`:

  ```csharp
  using Legion.Admin.Data.Models;
  using Legion.Admin.Data.Models.Prompts;

  namespace Legion.Admin.Data.Stores;

  public interface IPromptStore
  {
      Task<PromptVersion?> GetPublishedPromptAsync(string path, CancellationToken ct = default);
      Task<PromptVersion?> GetPromptVersionAsync(PromptVersionId id, CancellationToken ct = default);
      Task<List<PromptVersion>> GetAgentPromptsAsync(AgentOptionsId agentId, CancellationToken ct = default);
      Task<List<PromptVersion>> GetPromptHistoryAsync(PromptDefinitionId definitionId, CancellationToken ct = default);
      Task<List<PromptDefinition>> SearchDefinitionsAsync(string query, PromptType? typeFilter = null, bool includeDeleted = false, CancellationToken ct = default);
      Task<PromptDefinition> CreateDefinitionAsync(string path, PromptType type, PromptCategory category, bool isDefaultIncluded, string createdBy, CancellationToken ct = default);
      Task<PromptVersion> CreateDraftAsync(PromptDefinitionId definitionId, string content, string? frontmatter, string createdBy, string? notes, CancellationToken ct = default);
      Task UpdateDraftAsync(PromptVersionId draftId, string content, string? frontmatter, CancellationToken ct = default);
      Task PublishDraftAsync(PromptVersionId draftId, CancellationToken ct = default);
      Task DiscardDraftAsync(PromptVersionId draftId, CancellationToken ct = default);
      Task RepublishArchivedAsync(PromptVersionId archivedVersionId, CancellationToken ct = default);
      Task DeleteDefinitionAsync(PromptDefinitionId definitionId, CancellationToken ct = default);
      Task<List<AgentPromptAssignment>> GetAgentAssignmentsAsync(AgentOptionsId agentId, CancellationToken ct = default);
      Task SetAgentAssignmentsAsync(AgentOptionsId agentId, IEnumerable<(PromptDefinitionId definitionId, int order)> assignments, CancellationToken ct = default);
  }
  ```

- [ ] **Step 3: Create PromptStore implementation**

  Create `src/libs/Legion.Admin.Data/Stores/PromptStore.cs`:

  ```csharp
  using Legion.Admin.Data.Models;
  using Legion.Admin.Data.Models.Prompts;
  using Microsoft.EntityFrameworkCore;
  using YamlDotNet.Core;

  namespace Legion.Admin.Data.Stores;

  public class PromptStore(AppDbContext db) : IPromptStore
  {
      private static readonly System.Text.RegularExpressions.Regex PathRegex =
          new(@"^(/[\w\s\-]+)+$", System.Text.RegularExpressions.RegexOptions.Compiled);

      public async Task<PromptVersion?> GetPublishedPromptAsync(string path, CancellationToken ct = default) =>
          await db.PromptVersions.AsNoTracking()
              .Include(v => v.Definition)
              .Where(v => v.Definition!.Path == path
                       && v.Status == PromptStatus.Published
                       && v.Definition.DeletedAt == null)
              .FirstOrDefaultAsync(ct);

      public async Task<PromptVersion?> GetPromptVersionAsync(PromptVersionId id, CancellationToken ct = default) =>
          await db.PromptVersions.AsNoTracking()
              .Include(v => v.Definition)
              .FirstOrDefaultAsync(v => v.Id == id, ct);

      public async Task<List<PromptVersion>> GetAgentPromptsAsync(AgentOptionsId agentId, CancellationToken ct = default) =>
          await db.AgentPromptAssignments.AsNoTracking()
              .Where(a => a.AgentId == agentId)
              .Include(a => a.Definition)
                  .ThenInclude(d => d!.Versions)
              .OrderBy(a => a.Definition!.Category)
              .ThenBy(a => a.Order)
              .Select(a => a.Definition!.Versions
                  .First(v => v.Status == PromptStatus.Published))
              .ToListAsync(ct);

      public async Task<List<PromptVersion>> GetPromptHistoryAsync(PromptDefinitionId definitionId, CancellationToken ct = default) =>
          await db.PromptVersions.AsNoTracking()
              .Where(v => v.DefinitionId == definitionId)
              .OrderByDescending(v => v.CreatedAt)
              .ToListAsync(ct);

      public async Task<List<PromptDefinition>> SearchDefinitionsAsync(
          string query, PromptType? typeFilter = null, bool includeDeleted = false, CancellationToken ct = default)
      {
          var q = db.PromptDefinitions.AsNoTracking().AsQueryable();
          if (!includeDeleted) q = q.Where(d => d.DeletedAt == null);
          if (typeFilter.HasValue) q = q.Where(d => d.Type == typeFilter.Value);
          if (!string.IsNullOrWhiteSpace(query))
              q = q.Where(d => d.Path.Contains(query));
          return await q.OrderBy(d => d.Path).ToListAsync(ct);
      }

      public async Task<PromptDefinition> CreateDefinitionAsync(
          string path, PromptType type, PromptCategory category, bool isDefaultIncluded,
          string createdBy, CancellationToken ct = default)
      {
          ValidatePath(path);
          var existing = await db.PromptDefinitions.FirstOrDefaultAsync(d => d.Path == path, ct);
          if (existing is not null)
              throw new InvalidOperationException($"A prompt definition with path '{path}' already exists.");

          var definition = new PromptDefinition
          {
              Path = path,
              Type = type,
              Category = category,
              IsDefaultIncluded = isDefaultIncluded
          };
          db.PromptDefinitions.Add(definition);
          await db.SaveChangesAsync(ct);
          return definition;
      }

      public async Task<PromptVersion> CreateDraftAsync(
          PromptDefinitionId definitionId, string content, string? frontmatter,
          string createdBy, string? notes, CancellationToken ct = default)
      {
          if (string.IsNullOrWhiteSpace(content))
              throw new ArgumentException("Content cannot be empty.", nameof(content));

          var definition = await db.PromptDefinitions.FindAsync([definitionId], ct)
              ?? throw new InvalidOperationException($"Definition {definitionId} not found.");

          if (frontmatter is not null && definition.Type != PromptType.Prompt)
              ValidateYaml(frontmatter);

          var existingDraft = await db.PromptVersions
              .AnyAsync(v => v.DefinitionId == definitionId && v.Status == PromptStatus.Draft, ct);
          if (existingDraft)
              throw new InvalidOperationException(
                  $"Definition {definitionId} already has a Draft. Discard it before creating a new draft.");

          var version = new PromptVersion
          {
              DefinitionId = definitionId,
              Status = PromptStatus.Draft,
              Content = content,
              Frontmatter = frontmatter,
              CreatedBy = createdBy,
              Notes = notes
          };
          db.PromptVersions.Add(version);
          await db.SaveChangesAsync(ct);
          return version;
      }

      public async Task UpdateDraftAsync(
          PromptVersionId draftId, string content, string? frontmatter, CancellationToken ct = default)
      {
          var draft = await db.PromptVersions.FindAsync([draftId], ct);
          if (draft is null)
              throw new KeyNotFoundException($"Draft {draftId} not found.");
          if (draft.Status != PromptStatus.Draft)
              throw new InvalidOperationException($"Version {draftId} is not a Draft (status: {draft.Status}).");

          draft.Content = content;
          draft.Frontmatter = frontmatter;
          await db.SaveChangesAsync(ct);
      }

      public async Task PublishDraftAsync(PromptVersionId draftId, CancellationToken ct = default)
      {
          await using var tx = await db.Database.BeginTransactionAsync(
              System.Data.IsolationLevel.RepeatableRead, ct);

          var draft = await db.PromptVersions.FindAsync([draftId], ct)
              ?? throw new InvalidOperationException($"Draft {draftId} not found.");
          if (draft.Status != PromptStatus.Draft)
              throw new InvalidOperationException($"Version {draftId} is not a Draft.");

          var currentPublished = await db.PromptVersions
              .Where(v => v.DefinitionId == draft.DefinitionId && v.Status == PromptStatus.Published)
              .FirstOrDefaultAsync(ct);

          if (currentPublished is not null)
              currentPublished.Status = PromptStatus.Archived;

          draft.Status = PromptStatus.Published;
          await db.SaveChangesAsync(ct);
          await tx.CommitAsync(ct);
      }

      public async Task DiscardDraftAsync(PromptVersionId draftId, CancellationToken ct = default)
      {
          var draft = await db.PromptVersions.FindAsync([draftId], ct);
          if (draft is null)
              throw new KeyNotFoundException($"Draft {draftId} not found.");
          if (draft.Status != PromptStatus.Draft)
              throw new InvalidOperationException($"Version {draftId} is not a Draft.");

          db.PromptVersions.Remove(draft);
          await db.SaveChangesAsync(ct);
      }

      public async Task RepublishArchivedAsync(PromptVersionId archivedVersionId, CancellationToken ct = default)
      {
          await using var tx = await db.Database.BeginTransactionAsync(
              System.Data.IsolationLevel.RepeatableRead, ct);

          var archived = await db.PromptVersions.FindAsync([archivedVersionId], ct)
              ?? throw new InvalidOperationException($"Version {archivedVersionId} not found.");
          if (archived.Status != PromptStatus.Archived)
              throw new InvalidOperationException($"Version {archivedVersionId} is not Archived.");

          var currentPublished = await db.PromptVersions
              .Where(v => v.DefinitionId == archived.DefinitionId && v.Status == PromptStatus.Published)
              .FirstOrDefaultAsync(ct);

          if (currentPublished is not null)
              currentPublished.Status = PromptStatus.Archived;

          archived.Status = PromptStatus.Published;
          await db.SaveChangesAsync(ct);
          await tx.CommitAsync(ct);
      }

      public async Task DeleteDefinitionAsync(PromptDefinitionId definitionId, CancellationToken ct = default)
      {
          var definition = await db.PromptDefinitions.FindAsync([definitionId], ct)
              ?? throw new InvalidOperationException($"Definition {definitionId} not found.");
          definition.DeletedAt = DateTime.UtcNow;
          await db.SaveChangesAsync(ct);
      }

      public async Task<List<AgentPromptAssignment>> GetAgentAssignmentsAsync(
          AgentOptionsId agentId, CancellationToken ct = default) =>
          await db.AgentPromptAssignments.AsNoTracking()
              .Include(a => a.Definition)
              .Where(a => a.AgentId == agentId)
              .OrderBy(a => a.Definition!.Category)
              .ThenBy(a => a.Order)
              .ToListAsync(ct);

      public async Task SetAgentAssignmentsAsync(
          AgentOptionsId agentId,
          IEnumerable<(PromptDefinitionId definitionId, int order)> assignments,
          CancellationToken ct = default)
      {
          var existing = await db.AgentPromptAssignments
              .Where(a => a.AgentId == agentId)
              .ToListAsync(ct);
          db.AgentPromptAssignments.RemoveRange(existing);

          foreach (var (definitionId, order) in assignments)
          {
              db.AgentPromptAssignments.Add(new AgentPromptAssignment
              {
                  AgentId = agentId,
                  DefinitionId = definitionId,
                  Order = order
              });
          }
          await db.SaveChangesAsync(ct);
      }

      private static void ValidatePath(string path)
      {
          if (!PathRegex.IsMatch(path))
              throw new ArgumentException(
                  $"Path '{path}' is invalid. Must match ^(/[\\w\\s\\-]+)+$", nameof(path));
      }

      private static void ValidateYaml(string yaml)
      {
          try
          {
              var deserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
              deserializer.Deserialize<object>(yaml);
          }
          catch (YamlException ex)
          {
              throw new ArgumentException($"Frontmatter is not valid YAML: {ex.Message}", nameof(yaml));
          }
      }
  }
  ```

- [ ] **Step 4: Register IPromptStore in DI**

  In `src/libs/Legion.Admin.Data.Sqlite/Extensions/SqliteExtensions.cs`, add after the `ISecretsStore` registration:

  ```csharp
  services.AddScoped<IPromptStore, PromptStore>();
  ```

  Do the same in `src/libs/Legion.Admin.Data.PostgreSQL/Extensions/PostgreSqlExtensions.cs`.

- [ ] **Step 5: Build to confirm no errors**

  ```bash
  dotnet build src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj
  dotnet build src/libs/Legion.Admin.Data.Sqlite/Legion.Admin.Data.Sqlite.csproj
  dotnet build src/libs/Legion.Admin.Data.PostgreSQL/Legion.Admin.Data.PostgreSQL.csproj
  ```
  Expected: All build succeeded.

- [ ] **Step 6: Commit**

  ```bash
  git add src/libs/Legion.Admin.Data/Stores/IPromptStore.cs \
          src/libs/Legion.Admin.Data/Stores/PromptStore.cs \
          src/libs/Legion.Admin.Data.Sqlite/Extensions/SqliteExtensions.cs \
          src/libs/Legion.Admin.Data.PostgreSQL/Extensions/PostgreSqlExtensions.cs
  git commit -m "feat: add IPromptStore interface and PromptStore implementation"
  ```

---

## Task 7: Unit Tests for PromptStore

**Files:**
- Create: `tests/Legion.Admin.Data.Tests/Prompts/PromptStoreTests.cs`

> **Context:** Uses in-memory EF Core (`Microsoft.EntityFrameworkCore.InMemory` — already in the test project). Transactions with `IsolationLevel.RepeatableRead` are ignored by the in-memory provider, so `PublishDraftAsync` can be tested without a real database.

- [ ] **Step 1: Write the test file**

  Create `tests/Legion.Admin.Data.Tests/Prompts/PromptStoreTests.cs`:

  ```csharp
  using Legion.Admin.Data.Models;
  using Legion.Admin.Data.Models.Prompts;
  using Legion.Admin.Data.Stores;
  using Microsoft.EntityFrameworkCore;
  using Xunit;

  namespace Legion.Admin.Data.Tests.Prompts;

  public class PromptStoreTests
  {
      private static AppDbContext CreateDb()
      {
          var options = new DbContextOptionsBuilder<AppDbContext>()
              .UseInMemoryDatabase(Guid.NewGuid().ToString())
              .Options;
          return new AppDbContext(options);
      }

      // ── CreateDefinitionAsync ───────────────────────────────────

      [Fact]
      public async Task CreateDefinitionAsync_ValidPath_Succeeds()
      {
          await using var db = CreateDb();
          var store = new PromptStore(db);
          var def = await store.CreateDefinitionAsync(
              "/Agents/Prompts/Bash", PromptType.Prompt, PromptCategory.TaskSpecific,
              false, "test:Test User");

          Assert.Equal("/Agents/Prompts/Bash", def.Path);
          Assert.Equal(PromptType.Prompt, def.Type);
          Assert.Single(await db.PromptDefinitions.ToListAsync());
      }

      [Fact]
      public async Task CreateDefinitionAsync_InvalidPath_Throws()
      {
          await using var db = CreateDb();
          var store = new PromptStore(db);
          await Assert.ThrowsAsync<ArgumentException>(() =>
              store.CreateDefinitionAsync(
                  "no-leading-slash", PromptType.Prompt, PromptCategory.TaskSpecific,
                  false, "test:Test User"));
      }

      [Fact]
      public async Task CreateDefinitionAsync_DuplicatePath_Throws()
      {
          await using var db = CreateDb();
          var store = new PromptStore(db);
          await store.CreateDefinitionAsync("/Rules/Security", PromptType.Prompt, PromptCategory.Constraints, false, "test:User");
          await Assert.ThrowsAsync<InvalidOperationException>(() =>
              store.CreateDefinitionAsync("/Rules/Security", PromptType.Prompt, PromptCategory.Constraints, false, "test:User"));
      }

      // ── CreateDraftAsync ───────────────────────────────────────

      [Fact]
      public async Task CreateDraftAsync_Succeeds()
      {
          await using var db = CreateDb();
          var store = new PromptStore(db);
          var def = await store.CreateDefinitionAsync("/Test/Path", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");
          var draft = await store.CreateDraftAsync(def.Id, "# Hello", null, "test:User", null);

          Assert.Equal(PromptStatus.Draft, draft.Status);
          Assert.Equal("# Hello", draft.Content);
      }

      [Fact]
      public async Task CreateDraftAsync_SecondDraftConflicts()
      {
          await using var db = CreateDb();
          var store = new PromptStore(db);
          var def = await store.CreateDefinitionAsync("/Test/Path", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");
          await store.CreateDraftAsync(def.Id, "# First", null, "test:User", null);

          await Assert.ThrowsAsync<InvalidOperationException>(() =>
              store.CreateDraftAsync(def.Id, "# Second", null, "test:User", null));
      }

      [Fact]
      public async Task CreateDraftAsync_InvalidYamlFrontmatter_Throws()
      {
          await using var db = CreateDb();
          var store = new PromptStore(db);
          var def = await store.CreateDefinitionAsync("/Skills/Git", PromptType.Skill, PromptCategory.TaskSpecific, false, "test:User");

          await Assert.ThrowsAsync<ArgumentException>(() =>
              store.CreateDraftAsync(def.Id, "content", "name: [unclosed", "test:User", null));
      }

      // ── PublishDraftAsync ──────────────────────────────────────

      [Fact]
      public async Task PublishDraftAsync_ArchivesCurrentPublished()
      {
          await using var db = CreateDb();
          var store = new PromptStore(db);
          var def = await store.CreateDefinitionAsync("/Test/Path", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");

          var v1 = await store.CreateDraftAsync(def.Id, "# V1", null, "test:User", null);
          await store.PublishDraftAsync(v1.Id);

          var v2 = await store.CreateDraftAsync(def.Id, "# V2", null, "test:User", null);
          await store.PublishDraftAsync(v2.Id);

          var v1Reloaded = await db.PromptVersions.FindAsync([v1.Id]);
          var v2Reloaded = await db.PromptVersions.FindAsync([v2.Id]);

          Assert.Equal(PromptStatus.Archived, v1Reloaded!.Status);
          Assert.Equal(PromptStatus.Published, v2Reloaded!.Status);
      }

      [Fact]
      public async Task PublishDraftAsync_NoPreviousPublished_Succeeds()
      {
          await using var db = CreateDb();
          var store = new PromptStore(db);
          var def = await store.CreateDefinitionAsync("/Test/Path", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");
          var v1 = await store.CreateDraftAsync(def.Id, "# V1", null, "test:User", null);
          await store.PublishDraftAsync(v1.Id); // No conflict expected

          var published = await store.GetPublishedPromptAsync("/Test/Path");
          Assert.NotNull(published);
          Assert.Equal("# V1", published.Content);
      }

      // ── GetAgentPromptsAsync ───────────────────────────────────

      [Fact]
      public async Task GetAgentPromptsAsync_ReturnsSortedByCategoryThenOrder()
      {
          await using var db = CreateDb();
          var store = new PromptStore(db);
          var agentId = AgentOptionsId.New();

          var d1 = await store.CreateDefinitionAsync("/Rules/Security", PromptType.Prompt, PromptCategory.Constraints, false, "test:User");
          var v1 = await store.CreateDraftAsync(d1.Id, "# Security", null, "test:User", null);
          await store.PublishDraftAsync(v1.Id);

          var d2 = await store.CreateDefinitionAsync("/System/Identity", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");
          var v2 = await store.CreateDraftAsync(d2.Id, "# Identity", null, "test:User", null);
          await store.PublishDraftAsync(v2.Id);

          await store.SetAgentAssignmentsAsync(agentId, [
              (d1.Id, 0),
              (d2.Id, 0)
          ]);

          var prompts = await store.GetAgentPromptsAsync(agentId);
          Assert.Equal(2, prompts.Count);
          Assert.Equal("# Identity", prompts[0].Content); // Foundation comes before Constraints
          Assert.Equal("# Security", prompts[1].Content);
      }

      // ── RepublishArchivedAsync ─────────────────────────────────

      [Fact]
      public async Task RepublishArchivedAsync_ArchivesCurrentAndPromotesOld()
      {
          await using var db = CreateDb();
          var store = new PromptStore(db);
          var def = await store.CreateDefinitionAsync("/Test/Path", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");
          var v1 = await store.CreateDraftAsync(def.Id, "# V1", null, "test:User", null);
          await store.PublishDraftAsync(v1.Id);
          var v2 = await store.CreateDraftAsync(def.Id, "# V2", null, "test:User", null);
          await store.PublishDraftAsync(v2.Id);

          // v1 is now Archived, v2 is Published. Republish v1.
          await store.RepublishArchivedAsync(v1.Id);

          var v1After = await db.PromptVersions.FindAsync([v1.Id]);
          var v2After = await db.PromptVersions.FindAsync([v2.Id]);
          Assert.Equal(PromptStatus.Published, v1After!.Status);
          Assert.Equal(PromptStatus.Archived, v2After!.Status);
      }

      // ── DeleteDefinitionAsync ──────────────────────────────────

      [Fact]
      public async Task DeleteDefinitionAsync_SoftDeletes()
      {
          await using var db = CreateDb();
          var store = new PromptStore(db);
          var def = await store.CreateDefinitionAsync("/Test/Path", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");
          await store.DeleteDefinitionAsync(def.Id);

          var reloaded = await db.PromptDefinitions.FindAsync([def.Id]);
          Assert.NotNull(reloaded!.DeletedAt);

          var results = await store.SearchDefinitionsAsync("Test");
          Assert.Empty(results); // hidden by default

          var withDeleted = await store.SearchDefinitionsAsync("Test", includeDeleted: true);
          Assert.Single(withDeleted);
      }
  }
  ```

- [ ] **Step 2: Run tests to verify they fail (before implementation is complete)**

  ```bash
  dotnet test tests/Legion.Admin.Data.Tests/ --filter "FullyQualifiedName~PromptStoreTests" -v
  ```
  Expected: Tests fail with build error (PromptStore doesn't exist yet). If you're following TDD strictly, write the tests before Task 6 Step 3.

- [ ] **Step 3: Run tests after implementation (Task 6 Step 3 complete)**

  ```bash
  dotnet test tests/Legion.Admin.Data.Tests/ --filter "FullyQualifiedName~PromptStoreTests" -v
  ```
  Expected: All 9 tests pass.

- [ ] **Step 4: Commit**

  ```bash
  git add tests/Legion.Admin.Data.Tests/Prompts/PromptStoreTests.cs
  git commit -m "test: add unit tests for PromptStore"
  ```

---

## Task 8a: Audit AgentFactory Call Sites

**Files:**
- Modify: any file calling `AgentFactory.CreateAgentAsync` (discovered in this task)

> **Why this task exists:** Task 8 changes the `CreateAgentAsync` signature from `(AgentOptions, ct)` to `(AgentOptionsId, AgentOptions, ct)`. Every call site must be updated or the build will fail.

- [ ] **Step 1: Find all call sites**

  ```bash
  grep -rn "CreateAgentAsync" src/ --include="*.cs"
  ```

  For each result, note the file path, line number, and current argument list.

- [ ] **Step 2: Update each call site**

  For each call site found in Step 1, update the call to pass the agent's `AgentOptionsId` as the first argument. For example, if the old call was:

  ```csharp
  await agentFactory.CreateAgentAsync(options, ct);
  ```

  Change it to:

  ```csharp
  await agentFactory.CreateAgentAsync(agentId, options, ct);  // agentId: AgentOptionsId from context
  ```

  If the call site doesn't have an `AgentOptionsId` in scope, check whether it receives a `Guid` that can be cast: `(AgentOptionsId)agentGuid`.

- [ ] **Step 3: Update DI registration if needed**

  If `AgentFactory` is registered in the DI container with an explicit factory lambda that references `ISecretsManager`, it now also needs `IPromptStore`. Find the registration:

  ```bash
  grep -rn "AgentFactory" src/ --include="*.cs"
  ```

  If it's registered as `services.AddScoped<AgentFactory>()` (constructor injection, which is the common pattern here), no change is needed — DI resolves both dependencies automatically.

- [ ] **Step 4: Build to confirm**

  ```bash
  dotnet build src/Legion.Agents/Legion.Agents.csproj
  dotnet build src/WebDev/WebDev.csproj
  ```
  Expected: Build succeeded (no call-site errors).

- [ ] **Step 5: Commit any call-site updates**

  ```bash
  git add -p  # stage only call-site changes
  git commit -m "refactor: update CreateAgentAsync call sites to pass AgentOptionsId"
  ```

---

## Task 8: Update AgentOptions + AgentFactory

**Files:**
- Modify: `src/Legion.Agents/Providers/AgentOptions.cs`
- Modify: `src/Legion.Agents/Providers/AgentFactory.cs`

- [ ] **Step 1: Add ToolWhitelist and ToolBlacklist to AgentOptions**

  In `src/Legion.Agents/Providers/AgentOptions.cs`, add the two new properties:

  ```csharp
  using Microsoft.Extensions.AI;

  namespace Legion.Agents.Providers;

  public record AgentOptions
  {
      public string? Provider { get; init; }
      public string? ApiKey { get; init; }
      public string? Name { get; init; }
      public string? Description { get; init; }
      public string? Instructions { get; init; }
      public string? Model { get; init; }
      public List<string>? Tools { get; init; }
      public int? MaxTokens { get; init; }
      public List<string>? ToolWhitelist { get; init; }
      public List<string>? ToolBlacklist { get; init; }
  }
  ```

- [ ] **Step 2: Update AgentFactory to inject IPromptStore and assemble prompts**

  In `src/Legion.Agents/Providers/AgentFactory.cs`, replace the entire file:

  ```csharp
  using Legion.Admin.Data.Models;
  using Legion.Admin.Data.Services;
  using Legion.Admin.Data.Stores;
  using Microsoft.Agents.AI;

  namespace Legion.Agents.Providers;

  public sealed class AgentFactory(ISecretsManager secrets, IPromptStore promptStore)
  {
      public async Task<AIAgent> CreateAgentAsync(
          AgentOptionsId agentId, AgentOptions options, CancellationToken ct = default)
      {
          // Resolve secret references in API key
          if (secrets.IsSecretReference(options.ApiKey))
          {
              options = options with
              {
                  ApiKey = await secrets.ResolveAsync(
                      new SecretRequest { Path = options.ApiKey! }, ct)
              };
          }

          // Assemble assigned prompts into system instructions
          var prompts = await promptStore.GetAgentPromptsAsync(agentId, ct);
          if (prompts.Count > 0)
          {
              var sections = prompts.Select(p =>
                  $"<!-- prompt: {p.Definition?.Path ?? p.DefinitionId.ToString()} v={p.Id.Value.ToString("N")[..8]} -->\n{p.Content}");
              var assembled = string.Join("\n\n", sections);
              var inline = options.Instructions ?? string.Empty;
              options = options with
              {
                  Instructions = string.IsNullOrEmpty(inline) ? assembled : $"{assembled}\n\n{inline}"
              };
          }

          // Apply tool whitelist then blacklist
          if (options.Tools is not null)
          {
              var filtered = options.Tools
                  .Where(t => options.ToolWhitelist is null or { Count: 0 } || options.ToolWhitelist.Contains(t))
                  .Where(t => options.ToolBlacklist is null || !options.ToolBlacklist.Contains(t))
                  .ToList();
              options = options with { Tools = filtered };
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

- [ ] **Step 3: Build to confirm no errors**

  ```bash
  dotnet build src/Legion.Agents/Legion.Agents.csproj
  ```
  Expected: Build succeeded.

- [ ] **Step 4: Commit**

  ```bash
  git add src/Legion.Agents/Providers/AgentOptions.cs \
          src/Legion.Agents/Providers/AgentFactory.cs
  git commit -m "feat: update AgentFactory to assemble assigned prompts and apply tool whitelist/blacklist"
  ```

---

## Task 9: Unit Tests for AgentFactory

**Files:**
- Modify: `src/Legion.Agents/Providers/AgentFactory.cs` — add `internal static AssembleOptions` helper
- Create: `tests/Legion.Admin.Data.Tests/Prompts/AgentFactoryPromptTests.cs`

> **Strategy:** Extract the options-assembly logic into an `internal static AgentOptions AssembleOptions(AgentOptions options, IList<PromptVersion> prompts)` helper in `AgentFactory`. Tests call this helper directly, bypassing the provider entirely. This produces meaningful assertions about prompt concatenation order, debug markers, inline instructions placement, and tool filtering.

- [ ] **Step 1: Extract AssembleOptions helper in AgentFactory**

  In `src/Legion.Agents/Providers/AgentFactory.cs`, add this `internal static` method and call it from `CreateAgentAsync`:

  ```csharp
  internal static AgentOptions AssembleOptions(AgentOptions options, IList<PromptVersion> prompts)
  {
      if (prompts.Count > 0)
      {
          var sections = prompts.Select(p =>
              $"<!-- prompt: {p.Definition?.Path ?? p.DefinitionId.ToString()} v={p.Id.Value.ToString("N")[..8]} -->\n{p.Content}");
          var assembled = string.Join("\n\n", sections);
          var inline = options.Instructions ?? string.Empty;
          options = options with
          {
              Instructions = string.IsNullOrEmpty(inline) ? assembled : $"{assembled}\n\n{inline}"
          };
      }

      if (options.Tools is not null)
      {
          var filtered = options.Tools
              .Where(t => options.ToolWhitelist is null or { Count: 0 } || options.ToolWhitelist.Contains(t))
              .Where(t => options.ToolBlacklist is null || !options.ToolBlacklist.Contains(t))
              .ToList();
          options = options with { Tools = filtered };
      }

      return options;
  }
  ```

  In `CreateAgentAsync`, replace the inline prompt-assembly and tool-filtering blocks with a single call:

  ```csharp
  var prompts = await promptStore.GetAgentPromptsAsync(agentId, ct);
  options = AssembleOptions(options, prompts);
  ```

- [ ] **Step 2: Build to confirm no errors**

  ```bash
  dotnet build src/Legion.Agents/Legion.Agents.csproj
  ```
  Expected: Build succeeded.

- [ ] **Step 3: Write the tests**

  Create `tests/Legion.Admin.Data.Tests/Prompts/AgentFactoryPromptTests.cs`:

  ```csharp
  using Legion.Admin.Data.Models;
  using Legion.Admin.Data.Models.Prompts;
  using Legion.Agents.Providers;
  using Xunit;

  namespace Legion.Admin.Data.Tests.Prompts;

  public class AgentFactoryPromptTests
  {
      private static PromptVersion MakeVersion(string path, PromptCategory category, string content)
      {
          var defId = PromptDefinitionId.New();
          return new PromptVersion
          {
              Id = PromptVersionId.New(),
              DefinitionId = defId,
              Status = PromptStatus.Published,
              Content = content,
              CreatedBy = "test:User",
              Definition = new PromptDefinition
              {
                  Id = defId,
                  Path = path,
                  Type = PromptType.Prompt,
                  Category = category
              }
          };
      }

      // ── Tool filtering ─────────────────────────────────────────

      [Fact]
      public void AssembleOptions_ToolWhitelistNull_AllToolsAllowed()
      {
          var options = new AgentOptions { Tools = ["bash", "read", "write"], ToolWhitelist = null };
          var result = AgentFactory.AssembleOptions(options, []);
          Assert.Equal(["bash", "read", "write"], result.Tools);
      }

      [Fact]
      public void AssembleOptions_ToolWhitelistEmpty_AllToolsAllowed()
      {
          var options = new AgentOptions { Tools = ["bash", "read", "write"], ToolWhitelist = [] };
          var result = AgentFactory.AssembleOptions(options, []);
          Assert.Equal(["bash", "read", "write"], result.Tools);
      }

      [Fact]
      public void AssembleOptions_ToolWhitelist_FiltersToWhitelistedOnly()
      {
          var options = new AgentOptions
          {
              Tools = ["bash", "read", "write"],
              ToolWhitelist = ["bash", "read"]
          };
          var result = AgentFactory.AssembleOptions(options, []);
          Assert.Equal(["bash", "read"], result.Tools);
      }

      [Fact]
      public void AssembleOptions_ToolBlacklist_RemovesBlacklistedTools()
      {
          var options = new AgentOptions
          {
              Tools = ["bash", "read", "write"],
              ToolBlacklist = ["write"]
          };
          var result = AgentFactory.AssembleOptions(options, []);
          Assert.DoesNotContain("write", result.Tools!);
          Assert.Contains("bash", result.Tools!);
          Assert.Contains("read", result.Tools!);
      }

      [Fact]
      public void AssembleOptions_BlacklistOverridesWhitelist()
      {
          var options = new AgentOptions
          {
              Tools = ["bash", "read", "write"],
              ToolWhitelist = ["bash", "write"],
              ToolBlacklist = ["write"]
          };
          var result = AgentFactory.AssembleOptions(options, []);
          Assert.Equal(["bash"], result.Tools);
      }

      // ── Prompt assembly ────────────────────────────────────────

      [Fact]
      public void AssembleOptions_NoPrompts_InstructionsUnchanged()
      {
          var options = new AgentOptions { Instructions = "original" };
          var result = AgentFactory.AssembleOptions(options, []);
          Assert.Equal("original", result.Instructions);
      }

      [Fact]
      public void AssembleOptions_OnePrompt_InstructionsSetToPromptContent()
      {
          var prompt = MakeVersion("/System/Identity", PromptCategory.Foundation, "You are an AI.");
          var options = new AgentOptions { Instructions = null };
          var result = AgentFactory.AssembleOptions(options, [prompt]);

          Assert.Contains("You are an AI.", result.Instructions);
          Assert.Contains("<!-- prompt: /System/Identity", result.Instructions);
      }

      [Fact]
      public void AssembleOptions_InlineInstructions_AppendedAfterAssembledPrompts()
      {
          var prompt = MakeVersion("/System/Identity", PromptCategory.Foundation, "You are an AI.");
          var options = new AgentOptions { Instructions = "My custom override." };
          var result = AgentFactory.AssembleOptions(options, [prompt]);

          var idx = result.Instructions!.IndexOf("You are an AI.");
          var overrideIdx = result.Instructions.IndexOf("My custom override.");
          Assert.True(idx < overrideIdx, "Assembled prompts should appear before inline instructions.");
      }

      [Fact]
      public void AssembleOptions_MultiplePrompts_ContainsDebugMarkersForEach()
      {
          var p1 = MakeVersion("/System/Identity", PromptCategory.Foundation, "Content A");
          var p2 = MakeVersion("/Rules/Security", PromptCategory.Constraints, "Content B");
          var options = new AgentOptions();
          var result = AgentFactory.AssembleOptions(options, [p1, p2]);

          Assert.Contains("<!-- prompt: /System/Identity", result.Instructions);
          Assert.Contains("<!-- prompt: /Rules/Security", result.Instructions);
          Assert.Contains("Content A", result.Instructions);
          Assert.Contains("Content B", result.Instructions);
      }

      [Fact]
      public void AssembleOptions_NullTools_ToolsRemainsNull()
      {
          var options = new AgentOptions { Tools = null };
          var result = AgentFactory.AssembleOptions(options, []);
          Assert.Null(result.Tools);
      }
  }
  ```

- [ ] **Step 4: Run tests to verify they fail (before AssembleOptions exists)**

  ```bash
  dotnet test tests/Legion.Admin.Data.Tests/ --filter "FullyQualifiedName~AgentFactoryPromptTests" -v
  ```
  Expected: Build error — `AgentFactory.AssembleOptions` does not exist yet.

- [ ] **Step 5: Add AssembleOptions helper (Step 1 above) then re-run**

  ```bash
  dotnet test tests/Legion.Admin.Data.Tests/ --filter "FullyQualifiedName~AgentFactoryPromptTests" -v
  ```
  Expected: All 9 tests pass.

- [ ] **Step 6: Commit**

  ```bash
  git add src/Legion.Agents/Providers/AgentFactory.cs \
          tests/Legion.Admin.Data.Tests/Prompts/AgentFactoryPromptTests.cs
  git commit -m "test: add AgentFactory prompt assembly and tool filtering tests"
  ```

---

## Task 10: PromptsController

**Files:**
- Create: `src/WebDev/Controllers/PromptsController.cs`

> **Context:** Follows the same pattern as `SecretsController`. Uses `[Authorize(Roles = "admin")]`. Path segments containing slashes use query parameters (`?path=`) not route parameters to avoid routing conflicts.

- [ ] **Step 1: Create PromptsController**

  Create `src/WebDev/Controllers/PromptsController.cs`:

  ```csharp
  using Legion.Admin.Data.Models;
  using Legion.Admin.Data.Models.Prompts;
  using Legion.Admin.Data.Stores;
  using Microsoft.AspNetCore.Authorization;
  using Microsoft.AspNetCore.Mvc;

  namespace WebDev.Controllers;

  [ApiController, Route("api/prompts")]
  [Authorize(Roles = "admin")]
  public class PromptsController(IPromptStore store) : ControllerBase
  {
      [HttpGet]
      public async Task<IActionResult> Search(
          [FromQuery] string? query = null,
          [FromQuery] string? type = null,
          [FromQuery] bool includeDeleted = false,
          CancellationToken ct = default)
      {
          PromptType? typeFilter = type is not null && Enum.TryParse<PromptType>(type, out var t) ? t : null;
          var defs = await store.SearchDefinitionsAsync(query ?? string.Empty, typeFilter, includeDeleted, ct);
          return Ok(defs);
      }

      [HttpGet("by-path")]
      public async Task<IActionResult> GetByPath([FromQuery] string path, CancellationToken ct = default)
      {
          var version = await store.GetPublishedPromptAsync(path, ct);
          return version is null ? NotFound() : Ok(version);
      }

      [HttpGet("by-path/history")]
      public async Task<IActionResult> GetHistory([FromQuery] string path, CancellationToken ct = default)
      {
          var defs = await store.SearchDefinitionsAsync(path, includeDeleted: true, ct: ct);
          var def = defs.FirstOrDefault(d => d.Path == path);
          if (def is null) return NotFound();
          var history = await store.GetPromptHistoryAsync(def.Id, ct);
          return Ok(history);
      }

      [HttpGet("{id:guid}")]
      public async Task<IActionResult> GetVersion(Guid id, CancellationToken ct = default)
      {
          var version = await store.GetPromptVersionAsync((PromptVersionId)id, ct);
          return version is null ? NotFound() : Ok(version);
      }

      [HttpPost("definitions")]
      public async Task<IActionResult> CreateDefinition(
          [FromBody] CreateDefinitionRequest req, CancellationToken ct = default)
      {
          try
          {
              var createdBy = $"{User.FindFirst("sub")?.Value}:{User.Identity?.Name}";
              var def = await store.CreateDefinitionAsync(req.Path, req.Type, req.Category, req.IsDefaultIncluded, createdBy, ct);
              return CreatedAtAction(nameof(GetByPath), new { path = def.Path }, def);
          }
          catch (ArgumentException ex) { return BadRequest(ex.Message); }
          catch (InvalidOperationException ex) { return Conflict(ex.Message); }
      }

      [HttpPost("drafts")]
      public async Task<IActionResult> CreateDraft(
          [FromBody] CreateDraftRequest req, CancellationToken ct = default)
      {
          try
          {
              var createdBy = $"{User.FindFirst("sub")?.Value}:{User.Identity?.Name}";
              var version = await store.CreateDraftAsync(req.DefinitionId, req.Content, req.Frontmatter, createdBy, req.Notes, ct);
              return CreatedAtAction(nameof(GetVersion), new { id = (Guid)version.Id }, version);
          }
          catch (ArgumentException ex) { return BadRequest(ex.Message); }
          catch (InvalidOperationException ex) { return Conflict(ex.Message); }
      }

      [HttpPut("drafts/{id:guid}")]
      public async Task<IActionResult> UpdateDraft(
          Guid id, [FromBody] UpdateDraftRequest req, CancellationToken ct = default)
      {
          try
          {
              await store.UpdateDraftAsync((PromptVersionId)id, req.Content, req.Frontmatter, ct);
              return NoContent();
          }
          catch (ArgumentException ex) { return BadRequest(ex.Message); }
          catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
          catch (InvalidOperationException ex) { return Conflict(ex.Message); }
      }

      [HttpPost("drafts/{id:guid}/publish")]
      public async Task<IActionResult> PublishDraft(Guid id, CancellationToken ct = default)
      {
          try
          {
              await store.PublishDraftAsync((PromptVersionId)id, ct);
              return NoContent();
          }
          catch (InvalidOperationException ex) { return Conflict(ex.Message); }
      }

      [HttpDelete("drafts/{id:guid}")]
      public async Task<IActionResult> DiscardDraft(Guid id, CancellationToken ct = default)
      {
          try
          {
              await store.DiscardDraftAsync((PromptVersionId)id, ct);
              return NoContent();
          }
          catch (KeyNotFoundException ex) { return NotFound(ex.Message); }
          catch (InvalidOperationException ex) { return Conflict(ex.Message); }
      }

      [HttpPost("{id:guid}/republish")]
      public async Task<IActionResult> RepublishArchived(Guid id, CancellationToken ct = default)
      {
          try
          {
              await store.RepublishArchivedAsync((PromptVersionId)id, ct);
              return NoContent();
          }
          catch (InvalidOperationException ex) { return Conflict(ex.Message); }
      }

      [HttpDelete("definitions")]
      public async Task<IActionResult> DeleteDefinition([FromQuery] string path, CancellationToken ct = default)
      {
          var defs = await store.SearchDefinitionsAsync(path, ct: ct);
          var def = defs.FirstOrDefault(d => d.Path == path);
          if (def is null) return NotFound();
          await store.DeleteDefinitionAsync(def.Id, ct);
          return NoContent();
      }
  }

  public record CreateDefinitionRequest(string Path, PromptType Type, PromptCategory Category, bool IsDefaultIncluded);
  public record CreateDraftRequest(PromptDefinitionId DefinitionId, string Content, string? Frontmatter, string? Notes);
  public record UpdateDraftRequest(string Content, string? Frontmatter);
  ```

- [ ] **Step 2: Build to confirm no errors**

  ```bash
  dotnet build src/WebDev/WebDev.csproj
  ```
  Expected: Build succeeded.

- [ ] **Step 3: Commit**

  ```bash
  git add src/WebDev/Controllers/PromptsController.cs
  git commit -m "feat: add PromptsController for CRUD on prompt definitions and versions"
  ```

---

## Task 11: ImportController (with SSRF Protection)

**Files:**
- Create: `src/WebDev/Controllers/ImportController.cs`

> **SSRF protection:** The import endpoint fetches user-supplied URLs. The guard blocks loopback, RFC-1918, link-local, cloud metadata (169.254.169.254), IPv6 loopback, and non-https schemes. Check is performed after DNS resolution to prevent DNS rebinding.

- [ ] **Step 1: Create ImportController**

  Create `src/WebDev/Controllers/ImportController.cs`:

  ```csharp
  using System.Net;
  using System.Net.Http.Headers;
  using System.Text.RegularExpressions;
  using Legion.Admin.Data.Models.Prompts;
  using Legion.Admin.Data.Stores;
  using Microsoft.AspNetCore.Authorization;
  using Microsoft.AspNetCore.Mvc;

  namespace WebDev.Controllers;

  [ApiController, Route("api/prompts/import")]
  [Authorize(Roles = "admin")]
  public class ImportController(IPromptStore store, IHttpClientFactory httpClientFactory) : ControllerBase
  {
      private const long MaxResponseBytes = 10 * 1024 * 1024; // 10 MB
      private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(15);

      private static readonly Regex FilenamePrefixPattern =
          new(@"^(?:agent-prompt|skill|tool-description)-(.+)\.md$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

      [HttpPost]
      public async Task<IActionResult> Import([FromBody] ImportRequest req, CancellationToken ct = default)
      {
          string content;
          string suggestedFilename;

          if (req.Url is not null)
          {
              if (!Uri.TryCreate(req.Url, UriKind.Absolute, out var uri))
                  return BadRequest("Invalid URL.");
              if (uri.Scheme != "https")
                  return BadRequest("Only https:// URLs are allowed.");

              var ssrfError = await CheckSsrfAsync(uri);
              if (ssrfError is not null)
                  return BadRequest(ssrfError);

              var client = httpClientFactory.CreateClient("import");
              using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
              cts.CancelAfter(FetchTimeout);
              try
              {
                  var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, cts.Token);
                  response.EnsureSuccessStatusCode();
                  if (response.Content.Headers.ContentLength is long len && len > MaxResponseBytes)
                      return BadRequest("Response exceeds 10 MB limit.");
                  await using var stream = await response.Content.ReadAsStreamAsync(cts.Token);
                  using var reader = new System.IO.StreamReader(stream);
                  var sb = new System.Text.StringBuilder();
                  var buf = new char[8192];
                  int n;
                  while ((n = await reader.ReadAsync(buf, cts.Token)) > 0)
                  {
                      sb.Append(buf, 0, n);
                      if (sb.Length > MaxResponseBytes) return BadRequest("Response exceeds 10 MB limit.");
                  }
                  content = sb.ToString();
              }
              catch (OperationCanceledException) { return StatusCode(504, "Fetch timed out."); }
              catch (HttpRequestException ex) { return BadRequest($"Fetch failed: {ex.Message}"); }

              suggestedFilename = Path.GetFileName(uri.LocalPath);
          }
          else if (req.MarkdownContent is not null)
          {
              content = req.MarkdownContent;
              suggestedFilename = req.Filename ?? "paste.md";
          }
          else
          {
              return BadRequest("Provide either 'url' or 'markdownContent'.");
          }

          var preview = ParseImportPreview(content, suggestedFilename);
          return Ok(preview);
      }

      [HttpPost("confirm")]
      public async Task<IActionResult> ConfirmImport(
          [FromBody] ConfirmImportRequest req, CancellationToken ct = default)
      {
          var createdBy = $"{User.FindFirst("sub")?.Value}:{User.Identity?.Name}";
          var results = new List<object>();

          foreach (var item in req.Items)
          {
              try
              {
                  PromptDefinition def;
                  var existing = (await store.SearchDefinitionsAsync(item.Path, ct: ct))
                      .FirstOrDefault(d => d.Path == item.Path);
                  if (existing is null)
                      def = await store.CreateDefinitionAsync(item.Path, item.Type, item.Category, false, createdBy, ct);
                  else
                      def = existing;

                  var (frontmatter, body) = SplitFrontmatter(item.Content);
                  var version = await store.CreateDraftAsync(def.Id, body, frontmatter, createdBy, "Imported", ct);

                  if (req.PublishImmediately)
                      await store.PublishDraftAsync(version.Id, ct);

                  results.Add(new { path = item.Path, status = "ok", versionId = (Guid)version.Id });
              }
              catch (Exception ex)
              {
                  results.Add(new { path = item.Path, status = "error", message = ex.Message });
              }
          }

          return Ok(results);
      }

      private static ImportPreview ParseImportPreview(string content, string filename)
      {
          var (frontmatter, body) = SplitFrontmatter(content);
          var (suggestedPath, suggestedType) = InferPathAndType(filename);
          PromptCategory category = InferCategory(frontmatter);

          return new ImportPreview(
              Filename: filename,
              SuggestedPath: suggestedPath,
              Type: suggestedType,
              Category: category,
              Content: content,
              FrontmatterDetected: frontmatter is not null
          );
      }

      private static (string? frontmatter, string body) SplitFrontmatter(string content)
      {
          var lines = content.Split('\n');
          if (lines.Length < 2 || lines[0].Trim() != "---") return (null, content);
          var end = Array.FindIndex(lines, 1, l => l.Trim() == "---");
          if (end < 0) return (null, content);
          var frontmatter = string.Join('\n', lines[1..end]).Trim();
          var body = string.Join('\n', lines[(end + 1)..]).TrimStart('\n');
          return (frontmatter, body);
      }

      private static (string path, PromptType type) InferPathAndType(string filename)
      {
          var name = Path.GetFileNameWithoutExtension(filename);
          if (name.StartsWith("agent-prompt-", StringComparison.OrdinalIgnoreCase))
              return ($"/Agents/Prompts/{Capitalize(name[13..])}", PromptType.Prompt);
          if (name.StartsWith("skill-", StringComparison.OrdinalIgnoreCase))
              return ($"/Skills/{Capitalize(name[6..])}", PromptType.Skill);
          if (name.StartsWith("tool-description-", StringComparison.OrdinalIgnoreCase))
              return ($"/Tools/{Capitalize(name[17..])}", PromptType.ToolDescription);
          return ($"/Imported/{Capitalize(name)}", PromptType.Prompt);
      }

      private static PromptCategory InferCategory(string? frontmatter) =>
          frontmatter?.Contains("Foundation", StringComparison.OrdinalIgnoreCase) == true ? PromptCategory.Foundation :
          frontmatter?.Contains("Constraints", StringComparison.OrdinalIgnoreCase) == true ? PromptCategory.Constraints :
          frontmatter?.Contains("Overrides", StringComparison.OrdinalIgnoreCase) == true ? PromptCategory.Overrides :
          PromptCategory.TaskSpecific;

      private static string Capitalize(string s) =>
          string.IsNullOrEmpty(s) ? s :
          string.Join("-", s.Split('-').Select(p => char.ToUpperInvariant(p[0]) + p[1..]));

      private static async Task<string?> CheckSsrfAsync(Uri uri)
      {
          IPAddress[] addresses;
          try { addresses = await Dns.GetHostAddressesAsync(uri.Host); }
          catch { return $"Cannot resolve host '{uri.Host}'."; }

          foreach (var ip in addresses)
          {
              if (IsBlockedIp(ip))
                  return $"Requests to {ip} are not allowed.";
          }
          return null;
      }

      private static bool IsBlockedIp(IPAddress ip)
      {
          if (IPAddress.IsLoopback(ip)) return true;
          if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
              return ip.Equals(IPAddress.IPv6Loopback) || ip.IsIPv6LinkLocal;

          var bytes = ip.GetAddressBytes();
          return bytes[0] == 10 ||
                 (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) ||
                 (bytes[0] == 192 && bytes[1] == 168) ||
                 (bytes[0] == 169 && bytes[1] == 254); // link-local + metadata
      }
  }

  public record ImportRequest(string? Url, string? MarkdownContent, string? Filename);
  public record ImportPreview(string Filename, string SuggestedPath, PromptType Type, PromptCategory Category, string Content, bool FrontmatterDetected);
  public record ConfirmImportItem(string Path, PromptType Type, PromptCategory Category, string Content);
  public record ConfirmImportRequest(List<ConfirmImportItem> Items, bool PublishImmediately);
  ```

- [ ] **Step 2: Register IHttpClientFactory and named client in Program.cs or WebDev startup**

  In `src/WebDev/Program.cs` (or wherever services are registered), add:

  ```csharp
  builder.Services.AddHttpClient("import");
  ```

- [ ] **Step 3: Build to confirm no errors**

  ```bash
  dotnet build src/WebDev/WebDev.csproj
  ```
  Expected: Build succeeded.

- [ ] **Step 4: Commit**

  ```bash
  git add src/WebDev/Controllers/ImportController.cs src/WebDev/Program.cs
  git commit -m "feat: add ImportController with SSRF protection for markdown URL import"
  ```

---

## Task 12: AgentPromptsController

**Files:**
- Create: `src/WebDev/Controllers/AgentPromptsController.cs`

- [ ] **Step 1: Create AgentPromptsController**

  Create `src/WebDev/Controllers/AgentPromptsController.cs`:

  ```csharp
  using Legion.Admin.Data.Models;
  using Legion.Admin.Data.Models.Prompts;
  using Legion.Admin.Data.Stores;
  using Microsoft.AspNetCore.Authorization;
  using Microsoft.AspNetCore.Mvc;

  namespace WebDev.Controllers;

  [ApiController, Route("api/agents/{agentId:guid}/prompts")]
  [Authorize(Roles = "admin")]
  public class AgentPromptsController(IPromptStore store) : ControllerBase
  {
      [HttpGet]
      public async Task<IActionResult> GetAssignments(Guid agentId, CancellationToken ct = default)
      {
          var assignments = await store.GetAgentAssignmentsAsync((AgentOptionsId)agentId, ct);
          return Ok(assignments);
      }

      [HttpPost]
      public async Task<IActionResult> SetAssignments(
          Guid agentId, [FromBody] List<AssignmentItem> items, CancellationToken ct = default)
      {
          await store.SetAgentAssignmentsAsync(
              (AgentOptionsId)agentId,
              items.Select(i => ((PromptDefinitionId)i.DefinitionId, i.Order)),
              ct);
          return NoContent();
      }
  }

  public record AssignmentItem(Guid DefinitionId, int Order);
  ```

- [ ] **Step 2: Build to confirm no errors**

  ```bash
  dotnet build src/WebDev/WebDev.csproj
  ```
  Expected: Build succeeded.

- [ ] **Step 3: Commit**

  ```bash
  git add src/WebDev/Controllers/AgentPromptsController.cs
  git commit -m "feat: add AgentPromptsController for bulk-updating prompt assignments"
  ```

---

## Task 13: PromptEditor Razor Component

**Files:**
- Create: `src/WebDev/Components/Shared/PromptEditor.razor`

> **Context:** Reusable editor for draft markdown content and optional YAML frontmatter. Emits `OnSave` when the user clicks Save/Auto-save. Uses `RadzenTextArea` for the markdown body and optionally a second `RadzenTextArea` for YAML frontmatter (shown only for Skill/ToolDescription types). Auto-save is debounced via a `System.Timers.Timer`.

- [ ] **Step 1: Create PromptEditor.razor**

  Create `src/WebDev/Components/Shared/PromptEditor.razor`:

  ```razor
  @using Legion.Admin.Data.Models.Prompts
  @implements IDisposable

  <RadzenStack Orientation="Orientation.Vertical" Gap="0.75rem">
      @if (ShowFrontmatter)
      {
          <RadzenFormField Text="Frontmatter (YAML)">
              <RadzenTextArea @bind-Value="frontmatter" Rows="5" Style="font-family: monospace; width: 100%;"
                              @oninput="OnContentChanged" Placeholder="name: My Skill&#10;description: ...&#10;tags: [tag1, tag2]" />
          </RadzenFormField>
      }
      <RadzenFormField Text="Content (Markdown)">
          <RadzenTextArea @bind-Value="content" Rows="20" Style="font-family: monospace; width: 100%;"
                          @oninput="OnContentChanged" Placeholder="# Your prompt content here..." />
      </RadzenFormField>
      <RadzenStack Orientation="Orientation.Horizontal" Gap="0.5rem">
          <RadzenButton Text="Save Draft" Icon="save" Click="SaveNow" IsBusy="@saving" />
          @if (OnPublish.HasDelegate)
          {
              <RadzenButton Text="Publish" Icon="publish" ButtonStyle="ButtonStyle.Success"
                            Click="PublishNow" />
          }
          @if (OnDiscard.HasDelegate)
          {
              <RadzenButton Text="Discard Draft" Icon="delete_outline" ButtonStyle="ButtonStyle.Danger"
                            Click="DiscardNow" />
          }
          @if (statusMessage is not null)
          {
              <RadzenText TextStyle="TextStyle.Caption">@statusMessage</RadzenText>
          }
      </RadzenStack>
  </RadzenStack>

  @code {
      [Parameter] public string? InitialContent { get; set; }
      [Parameter] public string? InitialFrontmatter { get; set; }
      [Parameter] public bool ShowFrontmatter { get; set; }
      [Parameter] public EventCallback<(string content, string? frontmatter)> OnSave { get; set; }
      [Parameter] public EventCallback OnPublish { get; set; }
      [Parameter] public EventCallback OnDiscard { get; set; }

      private string content = string.Empty;
      private string? frontmatter;
      private bool saving;
      private string? statusMessage;
      private System.Timers.Timer? debounceTimer;
      private bool dirty;

      protected override void OnParametersSet()
      {
          content = InitialContent ?? string.Empty;
          frontmatter = InitialFrontmatter;
      }

      private void OnContentChanged(ChangeEventArgs _)
      {
          dirty = true;
          debounceTimer?.Dispose();
          debounceTimer = new System.Timers.Timer(2000);
          debounceTimer.Elapsed += async (_, _) =>
          {
              debounceTimer?.Dispose();
              await InvokeAsync(SaveNow);
          };
          debounceTimer.AutoReset = false;
          debounceTimer.Start();
      }

      private async Task SaveNow()
      {
          if (!dirty) return;
          saving = true;
          statusMessage = null;
          StateHasChanged();
          await OnSave.InvokeAsync((content, frontmatter));
          dirty = false;
          saving = false;
          statusMessage = $"Saved {DateTime.Now:HH:mm:ss}";
          StateHasChanged();
      }

      private async Task PublishNow() => await OnPublish.InvokeAsync();
      private async Task DiscardNow() => await OnDiscard.InvokeAsync();

      public void Dispose() => debounceTimer?.Dispose();
  }
  ```

- [ ] **Step 2: Build to confirm no errors**

  ```bash
  dotnet build src/WebDev/WebDev.csproj
  ```
  Expected: Build succeeded.

- [ ] **Step 3: Commit**

  ```bash
  git add src/WebDev/Components/Shared/PromptEditor.razor
  git commit -m "feat: add PromptEditor reusable Blazor component with debounced auto-save"
  ```

---

## Task 14: PromptLibrary Page

**Files:**
- Create: `src/WebDev/Components/Pages/Prompts.razor`

> **Context:** Mirrors the structure of `Secrets.razor` — left pane with search + tree, right pane with detail + editor. The tree is built from `Path` strings split on `/`. Status badges use text labels. Publishes via the `PromptsController` API using `HttpClient` (`NavigationManager` or an injected typed client).

- [ ] **Step 1: Create Prompts.razor**

  Create `src/WebDev/Components/Pages/Prompts.razor`:

  ```razor
  @page "/admin/prompts"
  @using Microsoft.AspNetCore.Authorization
  @using Legion.Admin.Data.Models.Prompts
  @using Legion.Admin.Data.Stores
  @layout Legion.Admin.UI.Layouts.MainLayout
  @rendermode InteractiveServer
  @attribute [Authorize(Roles = "admin")]
  @inject IPromptStore PromptStore
  @inject NotificationService NotificationService
  @inject DialogService DialogService

  <PageTitle>Prompt Library</PageTitle>
  <RadzenText TextStyle="TextStyle.H4" TagName="TagName.H1" class="rz-mb-4">Prompt Library</RadzenText>

  <RadzenRow>
      <RadzenColumn Size="4">
          <RadzenStack Orientation="Orientation.Horizontal" Gap="0.5rem" class="rz-mb-2">
              <RadzenTextBox Placeholder="Search…" @oninput="OnSearch" style="flex: 1;" />
              <RadzenButton Text="Import" Icon="upload" ButtonStyle="ButtonStyle.Light" Click="OpenImport" />
          </RadzenStack>
          <RadzenStack Orientation="Orientation.Horizontal" Gap="0.25rem" class="rz-mb-2">
              @foreach (var f in new[] { "All", "Prompt", "Skill", "ToolDescription" })
              {
                  <RadzenButton Text="@f" Size="ButtonSize.Small"
                                ButtonStyle="@(typeFilter == f ? ButtonStyle.Primary : ButtonStyle.Light)"
                                Click="@(() => SetTypeFilter(f))" />
              }
              <RadzenCheckBox @bind-Value="showDeleted" TValue="bool" Change="@(async _ => await LoadAsync())" />
              <RadzenLabel Text="Archived" />
          </RadzenStack>
          <RadzenTree Data="@rootNodes" @bind-Value="selected" Expand="OnExpand"
                      Style="width: 100%; height: 600px; overflow: auto; border: 1px solid var(--rz-base-300);">
              <RadzenTreeLevel TextProperty="Label" HasChildren="@(n => ((PromptTreeNode)n).IsFolder)" />
          </RadzenTree>
      </RadzenColumn>

      <RadzenColumn Size="8">
          @if (selected is PromptTreeNode { IsFolder: false } leaf && leaf.Definition is not null)
          {
              <PromptDetailPanel
                  Definition="leaf.Definition"
                  PromptStore="PromptStore"
                  OnChanged="LoadAsync"
                  NotificationService="NotificationService"
                  DialogService="DialogService" />
          }
          else
          {
              <RadzenCard>
                  <RadzenText>Select a prompt from the tree to view or edit.</RadzenText>
              </RadzenCard>
          }
      </RadzenColumn>
  </RadzenRow>

  @code {
      private List<PromptDefinition> allDefinitions = [];
      private List<PromptTreeNode> rootNodes = [];
      private object? selected;
      private string searchTerm = string.Empty;
      private string typeFilter = "All";
      private bool showDeleted;

      protected override async Task OnInitializedAsync() => await LoadAsync();

      private async Task LoadAsync()
      {
          PromptType? type = typeFilter == "All" ? null : Enum.Parse<PromptType>(typeFilter);
          allDefinitions = await PromptStore.SearchDefinitionsAsync(searchTerm, type, showDeleted);
          rootNodes = BuildTree(allDefinitions);
          await InvokeAsync(StateHasChanged);
      }

      private static List<PromptTreeNode> BuildTree(IEnumerable<PromptDefinition> defs)
      {
          var root = new Dictionary<string, PromptTreeNode>(StringComparer.OrdinalIgnoreCase);
          foreach (var def in defs)
          {
              var parts = def.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
              var current = root;
              string currentPath = string.Empty;
              for (int i = 0; i < parts.Length; i++)
              {
                  currentPath = i == 0 ? parts[i] : $"{currentPath}/{parts[i]}";
                  if (!current.TryGetValue(parts[i], out var node))
                  {
                      node = new PromptTreeNode
                      {
                          Label = parts[i],
                          FullPath = "/" + currentPath,
                          IsFolder = i < parts.Length - 1,
                          Definition = i == parts.Length - 1 ? def : null,
                          Children = []
                      };
                      current[parts[i]] = node;
                  }
                  current = node.Children.ToDictionary(c => c.Label, StringComparer.OrdinalIgnoreCase);
              }
          }
          return root.Values.OrderBy(n => n.Label).ToList();
      }

      private Task OnExpand(TreeExpandEventArgs args)
      {
          if (args.Value is PromptTreeNode node) args.Children.Data = node.Children;
          return Task.CompletedTask;
      }

      private void OnSearch(ChangeEventArgs e)
      {
          searchTerm = e.Value?.ToString() ?? string.Empty;
          rootNodes = BuildTree(allDefinitions.Where(d =>
              d.Path.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));
      }

      private async Task SetTypeFilter(string filter)
      {
          typeFilter = filter;
          await LoadAsync();
      }

      private async Task OpenImport()
      {
          var result = await DialogService.OpenAsync<ImportPromptDialog>("Import Prompts",
              new Dictionary<string, object?> { ["PromptStore"] = PromptStore });
          if (result is true) await LoadAsync();
      }

      private sealed class PromptTreeNode
      {
          public string Label { get; init; } = string.Empty;
          public string FullPath { get; init; } = string.Empty;
          public bool IsFolder { get; init; }
          public PromptDefinition? Definition { get; init; }
          public List<PromptTreeNode> Children { get; init; } = [];
      }
  }
  ```

  Create these two companion components now (they are referenced by `Prompts.razor`):

  `src/WebDev/Components/Shared/PromptDetailPanel.razor`:

  ```razor
  @using Legion.Admin.Data.Models.Prompts
  @using Legion.Admin.Data.Stores

  @if (Definition is not null)
  {
      <RadzenCard>
          <RadzenStack Orientation="Orientation.Vertical" Gap="0.5rem">
              <RadzenStack Orientation="Orientation.Horizontal" Gap="0.5rem" AlignItems="AlignItems.Center">
                  <RadzenText TextStyle="TextStyle.H6">@Definition.Path</RadzenText>
                  <RadzenBadge BadgeStyle="BadgeStyle.Info" Text="@Definition.Type.ToString()" />
                  <RadzenBadge BadgeStyle="BadgeStyle.Secondary" Text="@Definition.Category.ToString()" />
                  @if (publishedVersion is not null)
                  {
                      <RadzenBadge BadgeStyle="BadgeStyle.Success" Text="Published" />
                  }
                  else if (draftVersion is not null)
                  {
                      <RadzenBadge BadgeStyle="BadgeStyle.Warning" Text="Draft" />
                  }
              </RadzenStack>

              <RadzenStack Orientation="Orientation.Horizontal" Gap="0.5rem">
                  <RadzenButton Text="New Version" Icon="add" Size="ButtonSize.Small" Click="NewVersion" />
                  <RadzenButton Text="Edit Draft" Icon="edit" Size="ButtonSize.Small"
                                Disabled="@(draftVersion is null)" Click="EditDraft" />
                  <RadzenButton Text="View History" Icon="history" Size="ButtonSize.Small"
                                ButtonStyle="ButtonStyle.Light" Click="ViewHistory" />
                  <RadzenButton Text="Delete" Icon="delete" Size="ButtonSize.Small"
                                ButtonStyle="ButtonStyle.Danger" Click="DeleteDefinition" />
              </RadzenStack>

              @if (editingDraft && draftVersion is not null)
              {
                  <PromptEditor
                      InitialContent="@draftVersion.Content"
                      InitialFrontmatter="@draftVersion.Frontmatter"
                      ShowFrontmatter="@(Definition.Type != PromptType.Prompt)"
                      OnSave="SaveDraft"
                      OnPublish="PublishDraft"
                      OnDiscard="DiscardDraft" />
              }
              else if (publishedVersion is not null)
              {
                  @if (!string.IsNullOrEmpty(publishedVersion.Frontmatter))
                  {
                      <RadzenFormField Text="Frontmatter">
                          <pre style="font-family: monospace; font-size: 0.8rem;">@publishedVersion.Frontmatter</pre>
                      </RadzenFormField>
                  }
                  <RadzenFormField Text="Content">
                      <pre style="font-family: monospace; font-size: 0.8rem; white-space: pre-wrap;">@publishedVersion.Content</pre>
                  </RadzenFormField>
              }
              else
              {
                  <RadzenText>No published version. Create a draft to get started.</RadzenText>
              }
          </RadzenStack>
      </RadzenCard>
  }

  @code {
      [Parameter] public PromptDefinition? Definition { get; set; }
      [Parameter] public IPromptStore? PromptStore { get; set; }
      [Parameter] public EventCallback OnChanged { get; set; }
      [Parameter] public NotificationService? NotificationService { get; set; }
      [Parameter] public DialogService? DialogService { get; set; }

      private PromptVersion? publishedVersion;
      private PromptVersion? draftVersion;
      private bool editingDraft;

      protected override async Task OnParametersSetAsync() => await LoadVersionsAsync();

      private async Task LoadVersionsAsync()
      {
          if (Definition is null || PromptStore is null) return;
          var history = await PromptStore.GetPromptHistoryAsync(Definition.Id);
          publishedVersion = history.FirstOrDefault(v => v.Status == PromptStatus.Published);
          draftVersion = history.FirstOrDefault(v => v.Status == PromptStatus.Draft);
          editingDraft = false;
      }

      private void EditDraft() => editingDraft = true;

      private async Task NewVersion()
      {
          if (PromptStore is null || Definition is null) return;
          var sourceContent = publishedVersion?.Content ?? string.Empty;
          var sourceFrontmatter = publishedVersion?.Frontmatter;
          await PromptStore.CreateDraftAsync(Definition.Id, sourceContent, sourceFrontmatter, "ui:Admin", null);
          await LoadVersionsAsync();
          editingDraft = true;
          NotificationService?.Notify(NotificationSeverity.Info, "Draft created");
      }

      private async Task SaveDraft((string content, string? frontmatter) args)
      {
          if (PromptStore is null || draftVersion is null) return;
          await PromptStore.UpdateDraftAsync(draftVersion.Id, args.content, args.frontmatter);
          await LoadVersionsAsync();
          editingDraft = true;
      }

      private async Task PublishDraft()
      {
          if (PromptStore is null || draftVersion is null) return;
          await PromptStore.PublishDraftAsync(draftVersion.Id);
          await LoadVersionsAsync();
          NotificationService?.Notify(NotificationSeverity.Success, "Published");
          await OnChanged.InvokeAsync();
      }

      private async Task DiscardDraft()
      {
          if (PromptStore is null || draftVersion is null) return;
          if (DialogService is not null)
          {
              var ok = await DialogService.Confirm("Discard draft?", "Confirm");
              if (ok != true) return;
          }
          await PromptStore.DiscardDraftAsync(draftVersion.Id);
          await LoadVersionsAsync();
          NotificationService?.Notify(NotificationSeverity.Info, "Draft discarded");
      }

      private async Task ViewHistory()
      {
          if (PromptStore is null || Definition is null || DialogService is null) return;
          var history = await PromptStore.GetPromptHistoryAsync(Definition.Id);
          await DialogService.OpenAsync("Version History",
              ds => @<RadzenDataGrid Data="history" TItem="PromptVersion">
                  <Columns>
                      <RadzenDataGridColumn TItem="PromptVersion" Property="Status" Title="Status" />
                      <RadzenDataGridColumn TItem="PromptVersion" Property="CreatedAt" Title="Created" />
                      <RadzenDataGridColumn TItem="PromptVersion" Property="CreatedBy" Title="By" />
                      <RadzenDataGridColumn TItem="PromptVersion" Title="Actions">
                          <Template Context="v">
                              @if (v.Status == PromptStatus.Archived)
                              {
                                  <RadzenButton Text="Republish" Size="ButtonSize.Small" Click="@(async () => { await PromptStore.RepublishArchivedAsync(v.Id); ds.Close(); await LoadVersionsAsync(); await OnChanged.InvokeAsync(); })" />
                              }
                          </Template>
                      </RadzenDataGridColumn>
                  </Columns>
              </RadzenDataGrid>);
      }

      private async Task DeleteDefinition()
      {
          if (PromptStore is null || Definition is null) return;
          if (DialogService is not null)
          {
              var ok = await DialogService.Confirm($"Delete '{Definition.Path}'?", "Confirm Delete");
              if (ok != true) return;
          }
          await PromptStore.DeleteDefinitionAsync(Definition.Id);
          NotificationService?.Notify(NotificationSeverity.Warning, "Deleted");
          await OnChanged.InvokeAsync();
      }
  }
  ```

  `src/WebDev/Components/Shared/ImportPromptDialog.razor`:

  ```razor
  @using Legion.Admin.Data.Models.Prompts
  @inject DialogService DialogService
  @inject IHttpClientFactory HttpClientFactory

  <RadzenStack Orientation="Orientation.Vertical" Gap="0.75rem" Style="min-width: 600px;">
      <RadzenTabs>
          <Tabs>
              <RadzenTabsItem Text="Import from URL">
                  <RadzenFormField Text="https:// URL">
                      <RadzenTextBox @bind-Value="url" Placeholder="https://raw.githubusercontent.com/…" style="width: 100%;" />
                  </RadzenFormField>
                  <RadzenButton Text="Fetch & Preview" Icon="download" Click="FetchUrl" IsBusy="@loading" />
              </RadzenTabsItem>
              <RadzenTabsItem Text="Paste Markdown">
                  <RadzenFormField Text="Filename (optional)">
                      <RadzenTextBox @bind-Value="pasteFilename" Placeholder="skill-my-tool.md" style="width: 100%;" />
                  </RadzenFormField>
                  <RadzenFormField Text="Markdown content">
                      <RadzenTextArea @bind-Value="pasteContent" Rows="10" style="width: 100%; font-family: monospace;" />
                  </RadzenFormField>
                  <RadzenButton Text="Preview" Icon="visibility" Click="PreviewPaste" />
              </RadzenTabsItem>
          </Tabs>
      </RadzenTabs>

      @if (errorMessage is not null)
      {
          <RadzenAlert AlertStyle="AlertStyle.Danger">@errorMessage</RadzenAlert>
      }

      @if (previews.Count > 0)
      {
          <RadzenText TextStyle="TextStyle.Subtitle2">Preview</RadzenText>
          @foreach (var p in previews)
          {
              <RadzenCard Style="margin-bottom: 0.5rem;">
                  <RadzenStack Orientation="Orientation.Horizontal" Gap="0.5rem" AlignItems="AlignItems.Center">
                      <RadzenTextBox @bind-Value="p.SuggestedPath" style="flex: 1;" />
                      <RadzenBadge Text="@p.Type.ToString()" />
                      <RadzenBadge Text="@p.Category.ToString()" BadgeStyle="BadgeStyle.Secondary" />
                  </RadzenStack>
              </RadzenCard>
          }

          <RadzenStack Orientation="Orientation.Horizontal" Gap="0.5rem">
              <RadzenButton Text="Create as Draft" Click="@(() => Confirm(false))" />
              <RadzenButton Text="Create and Publish" ButtonStyle="ButtonStyle.Success" Click="@(() => Confirm(true))" />
          </RadzenStack>
      }
  </RadzenStack>

  @code {
      [Parameter] public IPromptStore? PromptStore { get; set; }

      private string? url;
      private string? pasteContent;
      private string? pasteFilename;
      private List<ImportPreviewItem> previews = [];
      private string? errorMessage;
      private bool loading;

      private async Task FetchUrl()
      {
          if (string.IsNullOrWhiteSpace(url)) return;
          loading = true; errorMessage = null;
          try
          {
              var client = HttpClientFactory.CreateClient("import");
              var resp = await client.PostAsJsonAsync("/api/prompts/import", new { url });
              if (!resp.IsSuccessStatusCode) { errorMessage = await resp.Content.ReadAsStringAsync(); return; }
              var preview = await resp.Content.ReadFromJsonAsync<ImportPreviewItem>();
              previews = preview is not null ? [preview] : [];
          }
          catch (Exception ex) { errorMessage = ex.Message; }
          finally { loading = false; }
      }

      private async Task PreviewPaste()
      {
          if (string.IsNullOrWhiteSpace(pasteContent)) return;
          var client = HttpClientFactory.CreateClient("import");
          var resp = await client.PostAsJsonAsync("/api/prompts/import",
              new { markdownContent = pasteContent, filename = pasteFilename ?? "paste.md" });
          if (!resp.IsSuccessStatusCode) { errorMessage = await resp.Content.ReadAsStringAsync(); return; }
          var preview = await resp.Content.ReadFromJsonAsync<ImportPreviewItem>();
          previews = preview is not null ? [preview] : [];
      }

      private async Task Confirm(bool publishImmediately)
      {
          if (PromptStore is null) return;
          var client = HttpClientFactory.CreateClient("import");
          var items = previews.Select(p => new
          {
              path = p.SuggestedPath,
              type = p.Type.ToString(),
              category = p.Category.ToString(),
              content = p.Content
          }).ToList();
          var resp = await client.PostAsJsonAsync("/api/prompts/import/confirm",
              new { items, publishImmediately });
          DialogService.Close(resp.IsSuccessStatusCode);
      }

      private record ImportPreviewItem(string Filename, string SuggestedPath, PromptType Type, PromptCategory Category, string Content, bool FrontmatterDetected);
  }
  ```

- [ ] **Step 2: Build to confirm no errors**

  ```bash
  dotnet build src/WebDev/WebDev.csproj
  ```
  Expected: Build succeeded.

- [ ] **Step 3: Commit**

  ```bash
  git add src/WebDev/Components/Pages/Prompts.razor \
          src/WebDev/Components/Shared/PromptDetailPanel.razor \
          src/WebDev/Components/Shared/ImportPromptDialog.razor
  git commit -m "feat: add PromptLibrary page at /admin/prompts with tree view"
  ```

---

## Task 15: PromptSelector Modal (Agent Configuration)

**Files:**
- Create: `src/WebDev/Components/Shared/PromptSelector.razor`

> **Context:** A `DialogService.OpenAsync<PromptSelector>()` modal rendered from the agent editor. Lists all active `PromptDefinition` records grouped by `Category`. Returns the selected `(PromptDefinitionId, Order)` pairs via `DialogService.Close()`. Preview pane shows concatenated prompt content with debug markers.

- [ ] **Step 1: Create PromptSelector.razor**

  Create `src/WebDev/Components/Shared/PromptSelector.razor`:

  ```razor
  @using Legion.Admin.Data.Models
  @using Legion.Admin.Data.Models.Prompts
  @using Legion.Admin.Data.Stores
  @inject IPromptStore PromptStore
  @inject DialogService DialogService

  <RadzenRow Style="height: 500px; overflow: hidden;">
      <RadzenColumn Size="6" Style="overflow-y: auto; border-right: 1px solid var(--rz-base-300); padding-right: 1rem;">
          <RadzenTextBox Placeholder="Search…" @oninput="OnSearch" style="width: 100%; margin-bottom: 0.5rem;" />
          @foreach (var category in categories)
          {
              var catDefs = FilteredByCategory(category);
              if (!catDefs.Any()) continue;
              <RadzenText TextStyle="TextStyle.Subtitle2">@category</RadzenText>
              @foreach (var def in catDefs)
              {
                  <RadzenStack Orientation="Orientation.Horizontal" AlignItems="AlignItems.Center" Gap="0.5rem">
                      <RadzenCheckBox Value="@IsSelected(def.Id)" TValue="bool"
                                      Change="@(v => ToggleSelection(def.Id, v))" />
                      <RadzenText Style="@(def.IsDefaultIncluded ? "font-weight: bold;" : "")">
                          @def.Path @(def.IsDefaultIncluded ? "★" : "")
                      </RadzenText>
                  </RadzenStack>
              }
          }
      </RadzenColumn>
      <RadzenColumn Size="6" Style="overflow-y: auto; padding-left: 1rem;">
          <RadzenText TextStyle="TextStyle.Subtitle2">Preview</RadzenText>
          <pre style="font-size: 0.75rem; white-space: pre-wrap;">@previewText</pre>
      </RadzenColumn>
  </RadzenRow>

  <RadzenStack Orientation="Orientation.Horizontal" Gap="0.5rem" class="rz-mt-3">
      <RadzenButton Text="Reset to Defaults" ButtonStyle="ButtonStyle.Light" Click="ResetToDefaults" />
      <RadzenButton Text="Select All" ButtonStyle="ButtonStyle.Light" Click="SelectAll" />
      <RadzenButton Text="Deselect All" ButtonStyle="ButtonStyle.Light" Click="DeselectAll" />
      <RadzenButton Text="Apply" ButtonStyle="ButtonStyle.Primary" Click="Apply" />
  </RadzenStack>

  @code {
      [Parameter] public AgentOptionsId AgentId { get; set; }

      private List<PromptDefinition> allDefinitions = [];
      private HashSet<PromptDefinitionId> selected = [];
      private Dictionary<PromptDefinitionId, int> orderMap = new();
      private string searchTerm = string.Empty;
      private string previewText = string.Empty;

      private static readonly PromptCategory[] categories =
          [PromptCategory.Foundation, PromptCategory.Constraints, PromptCategory.TaskSpecific, PromptCategory.Overrides];

      protected override async Task OnInitializedAsync()
      {
          allDefinitions = await PromptStore.SearchDefinitionsAsync(string.Empty);
          var existing = await PromptStore.GetAgentAssignmentsAsync(AgentId);
          selected = existing.Select(a => a.DefinitionId).ToHashSet();
          orderMap = existing.ToDictionary(a => a.DefinitionId, a => a.Order);
          UpdatePreview();
      }

      private IEnumerable<PromptDefinition> FilteredByCategory(PromptCategory category) =>
          allDefinitions
              .Where(d => d.Category == category &&
                          (string.IsNullOrEmpty(searchTerm) || d.Path.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)));

      private bool IsSelected(PromptDefinitionId id) => selected.Contains(id);

      private void ToggleSelection(PromptDefinitionId id, bool value)
      {
          if (value) { selected.Add(id); orderMap.TryAdd(id, selected.Count); }
          else { selected.Remove(id); orderMap.Remove(id); }
          UpdatePreview();
      }

      private void OnSearch(ChangeEventArgs e) => searchTerm = e.Value?.ToString() ?? string.Empty;

      private void ResetToDefaults()
      {
          selected = allDefinitions.Where(d => d.IsDefaultIncluded).Select(d => d.Id).ToHashSet();
          orderMap = selected.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
          UpdatePreview();
      }

      private void SelectAll()
      {
          selected = allDefinitions.Select(d => d.Id).ToHashSet();
          orderMap = selected.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
          UpdatePreview();
      }

      private void DeselectAll() { selected.Clear(); orderMap.Clear(); UpdatePreview(); }

      private void UpdatePreview()
      {
          var selectedDefs = allDefinitions
              .Where(d => selected.Contains(d.Id))
              .OrderBy(d => d.Category)
              .ThenBy(d => orderMap.TryGetValue(d.Id, out var o) ? o : 0);

          previewText = string.Join("\n\n", selectedDefs.Select(d =>
              $"<!-- prompt: {d.Path} -->\n[content of {d.Path}]"));
      }

      private async Task Apply()
      {
          var assignments = selected
              .Select(id => (id, orderMap.TryGetValue(id, out var o) ? o : 0))
              .ToList();
          await PromptStore.SetAgentAssignmentsAsync(AgentId, assignments);
          DialogService.Close(true);
      }
  }
  ```

- [ ] **Step 2: Build to confirm no errors**

  ```bash
  dotnet build src/WebDev/WebDev.csproj
  ```
  Expected: Build succeeded.

- [ ] **Step 3: Commit**

  ```bash
  git add src/WebDev/Components/Shared/PromptSelector.razor
  git commit -m "feat: add PromptSelector modal for agent prompt assignment"
  ```

---

## Self-Review Checklist

Run through this before declaring the plan complete:

| Spec Requirement | Task |
|-----------------|------|
| Versioned prompts with Draft/Published/Archived states | Task 2, 6 |
| UUID v7 branded IDs | Task 1 |
| Filtered unique index — one Published per definition | Task 3, 5 |
| `GetAgentPromptsAsync` sorted by Category then Order | Task 6, 7 |
| Publish is atomic (transaction) | Task 6 `PublishDraftAsync` — note: in-memory EF ignores isolation level; transaction is exercised only on real SQLite/PostgreSQL |
| Draft conflict check (409) | Task 6 `CreateDraftAsync` |
| Content-empty check | Task 6 `CreateDraftAsync` |
| YAML frontmatter validated on save | Task 6 `CreateDraftAsync` |
| `AgentOptions.ToolWhitelist / ToolBlacklist` | Task 8 |
| Tool filtering — empty whitelist = allow all, blacklist overrides | Task 8 `AgentFactory` |
| Prompt debug markers in assembled instructions | Task 8 `AgentFactory` |
| SSRF protection — https only, blocked IP ranges | Task 11 `ImportController` |
| 10 MB payload limit, 15s timeout | Task 11 |
| Filename → path/type inference on import | Task 11 |
| Draft/publish confirm flow on import | Task 11 `ConfirmImport` |
| `/admin/prompts` tree view with search + type filter | Task 14 |
| Prompt selector modal with category grouping + preview | Task 15 |
| IPromptStore registered in DI for SQLite + PostgreSQL | Task 6 |
| Migrations for both providers | Task 5 |
