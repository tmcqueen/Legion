# Markdown-Based Prompt, Skill, and Tool Import System — Design Spec

**Date:** 2026-04-30  
**Project:** Brigade (WebDev + Brigade.Agents + Brigade.Admin.Data)  
**Status:** Approved (rev 2 — Opus review applied)

---

## Overview

Users need a flexible way to manage, organize, and version system prompts, skills, and tool descriptions. This system introduces:

1. **Database-backed prompt library** — all prompts, skills, and tools stored as versioned markdown files
2. **Hierarchical tree organization** — free-form paths (e.g., `/Agents/Prompts/Bash-Command`)
3. **Category-based system prompt assembly** — automatic concatenation in the correct order (Foundation → Constraints → TaskSpecific → Overrides)
4. **Draft/publish workflow** — safe editing without breaking live agents
5. **Agent prompt selection UI** — agents explicitly choose which prompts to include
6. **Default inclusion** — prompts can be marked to auto-include in new agents

---

## Section 1: Architecture Overview & Data Model

### Core Entities

**PromptDefinition** — one row per logical prompt identity (path). Holds per-path metadata that never changes between versions.

Fields:
- `Path` — primary key. Hierarchical path (e.g., `/Agents/Prompts/Bash-Command`). Matches `^(/[\w\s-]+)+$` (allows word characters, spaces, and hyphens per segment, separated by `/`).
- `Type` — enum: `Prompt`, `Skill`, `ToolDescription`
- `Category` — enum: `Foundation`, `Constraints`, `TaskSpecific`, `Overrides`. Owned by the path definition; never changes between versions. To move a prompt to a different category, create a new path.
- `IsDefaultIncluded` — bool. When `true`, agents auto-include this prompt on creation.
- `CreatedAt` — DateTime (UTC)
- `DeletedAt` — DateTime? (UTC). Null = active path; set = soft-deleted (not shown in tree unless archived filter is enabled).

---

**PromptVersion** — immutable record for each version of a prompt, skill, or tool description. **Published and Archived versions are immutable.** Draft versions are mutable until published.

Fields:
- `Id` — primary key (GUID)
- `Path` — foreign key to `PromptDefinition.Path` (not null)
- `Status` — enum: `Draft`, `Published`, `Archived`. Replaces the old `IsActive` bool.
  - `Draft` — work in progress; mutable; not visible to agents
  - `Published` — the active version; immutable; used by agents at runtime
  - `Archived` — previously published; immutable; retained for history
- `Content` — markdown body (not null)
- `Frontmatter` — standard YAML fenced with `---` delimiters (nullable). Only used for `Skill` and `ToolDescription` types.
- `CreatedAt` — DateTime (UTC)
- `CreatedBy` — string storing `"{subjectId}:{displayName}"` (e.g., `"abc123:timm"`). Structured so display name changes don't lose the original author identity.
- `Notes` — optional user notes (e.g., "migrated from legacy system")

**Database constraint:** Filtered unique index on `(Path) WHERE Status = 'Published'` — ensures only one published version per path.
- PostgreSQL: `CREATE UNIQUE INDEX ix_prompt_versions_path_published ON prompt_versions (path) WHERE status = 'Published';`
- SQLite: `CREATE UNIQUE INDEX ix_prompt_versions_path_published ON prompt_versions (path) WHERE status = 'Published';`
- In EF Core `OnModelCreating`: `.HasIndex(v => v.Path).HasFilter("\"status\" = 'Published'").IsUnique()`

---

**AgentPromptAssignment** — junction table linking agents to the prompt definitions they include. Stores `DefinitionPath` (not a version ID), so agents always resolve the current published version at runtime.

Fields:
- `Id` — primary key (GUID)
- `AgentId` — foreign key to the Agent entity (verify exact table name during implementation)
- `DefinitionPath` — foreign key to `PromptDefinition.Path`
- `Order` — int. Controls display order within a category in the prompt selector UI. Also determines concatenation order **within** the same category (e.g., two `TaskSpecific` prompts are concatenated in ascending `Order`).

**Constraints:**
- Unique index: `(AgentId, DefinitionPath)`
- Cascade delete: if agent is deleted, its assignments are deleted

---

**AgentOptions** — updated fields

Additions:
- `ToolWhitelist` — `List<string>?`. Tool names allowed. If `null` or empty, all tools are allowed.
- `ToolBlacklist` — `List<string>?`. Tool names forbidden. Applied after whitelist; blacklist takes precedence over whitelist.

**Note:** `SelectedPromptPaths` is NOT stored on `AgentOptions`. The source of truth for an agent's prompt selection is the `AgentPromptAssignment` junction table. At runtime, `AgentFactory` queries the junction table by `AgentId` to get the list of paths, then resolves each to its current `Published` version.

---

### Data Flow: Agent Instantiation

1. Call `AgentFactory.CreateAgentAsync(agentId, agentOptions, cancellationToken)`
2. Resolve secrets (existing logic)
3. **NEW:** Fetch assigned prompts via `IPromptStore.GetAgentPromptsAsync(agentId, ct)`:
   - Returns published `PromptVersion` records joined from `AgentPromptAssignment` → `PromptDefinition` → `PromptVersion`
   - Sorted first by `PromptDefinition.Category` (Foundation=0, Constraints=1, TaskSpecific=2, Overrides=3), then by `AgentPromptAssignment.Order` within each category
4. **NEW:** Concatenate prompt content with debug markers:
   ```
   <!-- prompt: /Rules/Security v=3fa85f64 -->
   [content of /Rules/Security]

   <!-- prompt: /Agents/Prompts/Bash-Command v=7c4d2e91 -->
   [content of /Agents/Prompts/Bash-Command]
   ```
5. **NEW:** Merge concatenated prompts with inline `AgentOptions.Instructions` (inline instructions come last, as an override)
6. **NEW:** Apply tool whitelist/blacklist to available tools (see corrected logic below)
7. Create agent with final system prompt and filtered tools

---

## Section 2: Admin UI — Prompt Library Management

**Page:** `/admin/prompts`

**Access:** Admin-only (role-based, integrated with OpenIddict auth)

### Layout

**Left Pane (40%):**
- **Search box** — fuzzy search across `Path`, `Content`, `Frontmatter`
- **Filter toggles:** `[All] [Prompts] [Skills] [Tools]` with optional `[Show Archived]`
- **Hierarchical tree view:**
  - Tree built from `Path` by splitting on `/` (e.g., `/Agents/Prompts/Bash-Command` creates folders `Agents` → `Prompts` → `Bash-Command`)
  - Expandable folders (no checkbox on folders here — checkboxes appear only in the agent prompt selector)
  - Leaf items (prompts/skills/tools) show a status badge:
    - 🔵 Published (active)
    - ◯ Draft (work in progress)
    - ✓ Archived (previously published)
  - Right-click context menu:
    - "New Prompt/Skill/Tool Here" → opens editor with path pre-filled
    - "Rename Path" → only allowed if no agents currently have this path in their `AgentPromptAssignment`. Moves all versions to the new path.
    - "Delete" → soft-delete: sets `PromptDefinition.DeletedAt`, hidden from tree unless `[Show Archived]` is active

**Right Pane (60%):**

When no item selected:
- Empty state: "Select a prompt from the tree to view or edit"

When item selected:
- **Header:**
  - Path (read-only)
  - Type badge (Prompt / Skill / ToolDescription)
  - Category badge (Foundation / Constraints / TaskSpecific / Overrides)
  - Status badge: Published / Draft / Archived
- **Toolbar:**
  - "New Version" button → creates a `Draft` copy of the current published version
  - "Edit Draft" button (enabled only if a `Draft` exists for this path)
  - "View History" link → modal showing all versions (Draft, Published, Archived) with ability to re-publish any `Archived` version
  - "Delete Path" button → soft-delete
- **Content Display (read-only, when viewing Published or Archived):**
  - If Skill or ToolDescription: show `---`-fenced YAML frontmatter in a code block, then markdown content below
  - If Prompt: just markdown content
- **Draft Editor** (visible when in draft mode):
  - Markdown editor (syntax highlighting; live preview optional)
  - If Skill or ToolDescription: YAML editor (fenced `---` format) above the markdown content editor
  - Checkbox: "Include by default in new agents"
  - Auto-save to draft (debounced — saves to the `Draft` row's mutable content field)
  - "Publish" button → atomically archives the current `Published` version and sets this draft to `Published` (see publish transaction below)
  - "Discard Draft" button → deletes the `Draft` row

### Frontmatter Format

Skills and ToolDescriptions store metadata as standard YAML fenced with `---`:

```markdown
---
name: 'Tool Description: Bash (overview)'
description: Opening line of the Bash tool description
tags: [bash, shell]
---
Executes a given bash command and returns its output.
```

Prompts have no frontmatter section. The YAML parser on import should look for leading `---` fences first; if absent, treat as a plain Prompt.

### Import Workflow

**"Import Prompts" button** in the page toolbar.

**Security constraints on the import endpoint (`POST /api/prompts/import`):**
- `https://` scheme only — `http://` and `file://` are rejected
- Blocked destination ranges: loopback (127.0.0.0/8), private RFC-1918, link-local (169.254.0.0/16), IPv6 loopback (`::1`), cloud metadata IPs (169.254.169.254)
- Max response size: 10 MB per URL
- Fetch timeout: 15 seconds
- Admin role required

Dialog: "Import from URL"
- Input field: `https://` GitHub URL or direct markdown paste
- Processing (backend API endpoint handles parsing):
  - Fetches markdown files from provided URL (or accepts direct paste)
  - Detects frontmatter by looking for leading `---` fences
  - Suggests `DefinitionPath` based on filename convention:
    - `agent-prompt-bash-command.md` → `/Agents/Prompts/Bash-Command`
    - `skill-git-commit.md` → `/Skills/Git-Commit`
    - `tool-description-bash-overview.md` → `/Tools/Bash-Overview`
  - Infers `Type` from filename prefix (`agent-prompt-*` → Prompt, `skill-*` → Skill, `tool-description-*` → ToolDescription)
  - Infers `Category` from frontmatter or filename (defaults to `TaskSpecific`)
  - **Note:** Users can also task an agent with fetching and importing from external sources programmatically via the same API endpoint
- Preview table: `Filename`, `Suggested Path`, `Type`, `Category`
  - User can edit paths inline before confirming
- Buttons:
  - "Create as Draft" → creates `Draft` versions; user reviews and publishes individually
  - "Create and Publish" → creates versions directly as `Published`

---

## Section 3: Agent Configuration — Prompt Selection

**Integration:** In the agent creation/editing UI (WebDev), add a "System Prompts" section or tab.

### Prompt Selection Modal

Triggered by "Configure Prompts" button in agent editor.

**Layout:**
- Prompts organized under Category headings:
  - ▼ **Foundation**
    - ☐ `/System/Project-Context` (checked, default)
    - ☐ `/System/Core-Identity` (checked, default)
  - ▼ **Constraints**
    - ☐ `/Rules/Security` (checked, default)
    - ☐ `/Rules/Git-Safety`
  - ▼ **Task-Specific**
    - ☐ `/Agents/Prompts/Bash-Command`
    - ☐ `/Agents/Prompts/Code-Review`
  - ▼ **Overrides**
    - ☐ `/Custom/Project-Specific-Rules`

**Features:**
- Search box to filter by path
- Checkbox per leaf prompt (individual select)
- Sub-path group checkbox → selects all descendants under that path prefix
- Right pane: **Preview of concatenated result** (read-only, showing prompts in their final category order with debug markers)
- Default prompts shown in bold with a ★ indicator
- `Order` within a category is set by drag-and-drop in the right pane

**Buttons:**
- "Reset to Defaults" → re-selects all prompts where `PromptDefinition.IsDefaultIncluded = true`
- "Select All" → selects everything
- "Deselect All" → clears all
- "Apply" → bulk-updates `AgentPromptAssignment` for this agent

---

## Section 4: Implementation Architecture

### Publish Transaction

`PublishPromptVersionAsync` MUST wrap its work in a single database transaction at `RepeatableRead` (or higher) isolation to prevent race conditions. The preferred pattern is a single atomic UPDATE rather than two sequential queries:

```sql
-- PostgreSQL: atomically archive the old published version and publish the new draft
UPDATE prompt_versions
SET status = CASE
    WHEN id = @newVersionId THEN 'Published'
    WHEN status = 'Published' THEN 'Archived'
    ELSE status
END
WHERE path = @path AND (id = @newVersionId OR status = 'Published');
```

This ensures there is never a window where zero published versions exist for the path.

### IPromptStore Interface Placement

`IPromptStore` lives in `Brigade.Admin.Data.Services` (same namespace as `ISecretsManager`). The layering relationship (`Brigade.Agents` → `Brigade.Admin.Data`) already exists for `ISecretsManager` and will continue here. A future refactor could introduce a `Brigade.Admin.Abstractions` project to decouple the runtime from the data layer, but that is out of scope for this feature.

### IPromptStore Interface

```csharp
// Brigade.Admin.Data.Services
Task<PromptVersion?> GetPublishedPromptAsync(string path, CancellationToken ct = default);
Task<PromptVersion?> GetPromptVersionAsync(Guid id, CancellationToken ct = default);
Task<List<PromptVersion>> GetAgentPromptsAsync(Guid agentId, CancellationToken ct = default);
  // Returns published versions for agent's assignments, sorted by Category then Order
Task<List<PromptVersion>> GetPromptHistoryAsync(string path, CancellationToken ct = default);
  // Returns all versions (Draft, Published, Archived) newest first
Task<PromptVersion> CreateDraftAsync(
  string path, string content, string? frontmatter, string createdBy, string? notes,
  CancellationToken ct = default);
  // Creates a Draft version (mutable until published)
Task UpdateDraftAsync(Guid draftId, string content, string? frontmatter, CancellationToken ct = default);
  // Mutates an existing Draft (auto-save)
Task PublishDraftAsync(Guid draftId, CancellationToken ct = default);
  // Atomic: archives current Published, promotes Draft to Published
Task DiscardDraftAsync(Guid draftId, CancellationToken ct = default);
  // Deletes the Draft row
Task RepublishArchivedAsync(Guid archivedVersionId, CancellationToken ct = default);
  // Promotes an Archived version back to Published (archives current Published)
Task DeleteDefinitionAsync(string path, CancellationToken ct = default);
  // Soft-delete: sets PromptDefinition.DeletedAt
Task<List<PromptDefinition>> SearchDefinitionsAsync(
  string query, string? typeFilter = null, bool includeDeleted = false, CancellationToken ct = default);
  // Fuzzy search by path and content
Task<PromptDefinition> CreateDefinitionAsync(
  string path, PromptType type, PromptCategory category, bool isDefaultIncluded,
  string createdBy, CancellationToken ct = default);
  // Creates a new PromptDefinition (path must not already exist)
```

### WebDev (UI Layer)

**New Razor components:**

1. **`PromptLibrary.razor`** — `/admin/prompts` page
2. **`PromptSelector.razor`** — modal for agent configuration
3. **`PromptEditor.razor`** — reusable markdown + frontmatter editor with draft auto-save

**New API endpoints:**

Path segments containing slashes must use query parameters or catch-all routes:

- `GET /api/prompts` — list all definitions (with filter/search params: `?query=`, `?type=`, `?includeDeleted=`)
- `GET /api/prompts/by-path?path=/Agents/Prompts/Bash-Command` — get published version for path
- `GET /api/prompts/by-path/history?path=/Agents/Prompts/Bash-Command` — all versions for path
- `GET /api/prompts/{id}` — get specific version by GUID
- `POST /api/prompts/definitions` — create new `PromptDefinition`
- `POST /api/prompts/drafts` — create new draft version
- `PUT /api/prompts/drafts/{id}` — update draft content (auto-save)
- `POST /api/prompts/drafts/{id}/publish` — publish draft
- `DELETE /api/prompts/drafts/{id}` — discard draft
- `POST /api/prompts/{id}/republish` — republish an archived version
- `DELETE /api/prompts/definitions?path=...` — soft-delete a definition
- `POST /api/prompts/import` — import from URL or markdown text (HTTPS only, admin only)
- `GET /api/agents/{agentId}/prompts` — get prompt assignments for agent
- `POST /api/agents/{agentId}/prompts` — bulk update assignments (replace all)

### Brigade.Agents — Updated AgentFactory

```csharp
public async Task<AIAgent> CreateAgentAsync(Guid agentId, AgentOptions options, CancellationToken ct = default)
{
    // Existing: resolve secrets
    if (secrets.IsSecretReference(options.ApiKey))
        options = options with { ApiKey = await secrets.ResolveAsync(new SecretRequest { Path = options.ApiKey! }, ct) };

    // NEW: fetch and concatenate assigned prompts
    var prompts = await promptStore.GetAgentPromptsAsync(agentId, ct);
    if (prompts.Count > 0)
    {
        var sections = prompts.Select(p =>
            $"<!-- prompt: {p.Path} v={p.Id:N[..8]} -->\n{p.Content}");
        var assembled = string.Join("\n\n", sections);
        var inline = options.Instructions ?? "";
        options = options with { Instructions = string.IsNullOrEmpty(inline) ? assembled : $"{assembled}\n\n{inline}" };
    }

    // NEW: apply tool whitelist/blacklist
    if (options.Tools is not null)
    {
        var filtered = options.Tools
            .Where(t => options.ToolWhitelist is null or { Count: 0 } || options.ToolWhitelist.Contains(t))
            .Where(t => options.ToolBlacklist is null || !options.ToolBlacklist.Contains(t))
            .ToList();
        options = options with { Tools = filtered };
    }

    // Existing: create provider-specific agent
    return Enum.Parse<ProvidersEnum>(options.Provider ?? "UNSUPPORTED") switch
    {
        ProvidersEnum.MiniMax   => new MiniMaxProvider().CreateAgent(options),
        ProvidersEnum.Anthropic => new AnthropicProvider().CreateAgent(options),
        _ => throw new NotSupportedException($"The provider {options.Provider} is not supported.")
    };
}
```

### Caching

`GetAgentPromptsAsync` and `GetPublishedPromptAsync` are called on every agent creation. An in-memory cache (keyed by path) should be layered over the store in production. Cache entries must be invalidated when:
- A draft is published for that path
- A definition is soft-deleted
- An archived version is republished

Cache implementation is deferred to a follow-up; the `IPromptStore` interface is cache-transparent (implementations can wrap with `IMemoryCache` without changing callers).

---

## Section 5: Data Storage & Migration

**Two new tables.** The migration creates `prompt_definitions` first, then `prompt_versions` with its FK, then `agent_prompt_assignments`.

```csharp
// Up

// 1. Parent: one row per logical prompt path
CreateTable("prompt_definitions", table => new
{
    path               = table.Column<string>(maxLength: 500, nullable: false),
    type               = table.Column<string>(maxLength: 50,  nullable: false), // Prompt | Skill | ToolDescription
    category           = table.Column<string>(maxLength: 50,  nullable: false), // Foundation | Constraints | TaskSpecific | Overrides
    is_default_included = table.Column<bool>(nullable: false, defaultValue: false),
    created_at         = table.Column<DateTime>(nullable: false),
    deleted_at         = table.Column<DateTime>(nullable: true),
    PrimaryKey         = "pk_prompt_definitions"
});

// 2. Versions: one row per version of a path
CreateTable("prompt_versions", table => new
{
    id           = table.Column<Guid>(nullable: false),
    path         = table.Column<string>(maxLength: 500, nullable: false),  // FK → prompt_definitions.path
    status       = table.Column<string>(maxLength: 20, nullable: false),   // Draft | Published | Archived
    content      = table.Column<string>(nullable: false),
    frontmatter  = table.Column<string>(nullable: true),
    created_at   = table.Column<DateTime>(nullable: false),
    created_by   = table.Column<string>(maxLength: 512, nullable: false),  // "{subjectId}:{displayName}"
    notes        = table.Column<string>(nullable: true),
    PrimaryKey   = "pk_prompt_versions",
    ForeignKey   = ("path", "prompt_definitions", "path")
});

// Filtered unique index: only one Published version per path
// EF Core: .HasIndex(v => v.Path).HasFilter("\"status\" = 'Published'").IsUnique()
Sql("CREATE UNIQUE INDEX ix_prompt_versions_path_published ON prompt_versions (path) WHERE status = 'Published';");

// 3. Junction: which prompts each agent has selected
CreateTable("agent_prompt_assignments", table => new
{
    id              = table.Column<Guid>(nullable: false),
    agent_id        = table.Column<Guid>(nullable: false),           // FK → [AgentTable].id (verify name during implementation)
    definition_path = table.Column<string>(maxLength: 500, nullable: false), // FK → prompt_definitions.path
    order           = table.Column<int>(nullable: false, defaultValue: 0),
    PrimaryKey      = "pk_agent_prompt_assignments",
    ForeignKey      = ("definition_path", "prompt_definitions", "path")
});

CreateIndex("agent_prompt_assignments", new[] { "agent_id", "definition_path" }, unique: true);

// Down: drop in reverse dependency order
DropTable("agent_prompt_assignments");
DropTable("prompt_versions");
DropTable("prompt_definitions");
```

---

## Section 6: Error Handling & Validation

**Validation:**
- Path must match `^(/[\w\s-]+)+$` — forward slashes as separators; segments allow word chars, spaces, and hyphens
- Path segments must not be empty (no `//`)
- Content cannot be empty
- Frontmatter must be valid YAML (for Skill/ToolDescription); parse and validate before saving
- Only one `Published` version per path (enforced by filtered unique index)
- On `CreateDraftAsync`: if path already has a `Draft`, reject with `409 Conflict` — discard the existing draft first
- Category must match the path's `PromptDefinition.Category` if definition already exists

**Import security (enforced in `POST /api/prompts/import`):**
- Reject non-`https://` schemes (including `http://`, `file://`, `ftp://`)
- Block requests to: loopback (127.0.0.0/8), private (10.0.0.0/8, 172.16.0.0/12, 192.168.0.0/16), link-local (169.254.0.0/16), IPv6 loopback/local, cloud metadata endpoint (169.254.169.254)
- Maximum response body: 10 MB
- Request timeout: 15 seconds
- Requires admin role

**Runtime error handling:**
- Agent creation: if a path in `AgentPromptAssignment` has no `Published` version (e.g., all drafts), skip it with a warning log — do not fail agent creation
- Publish race: if the atomic UPDATE affects 0 rows, return `409 Conflict` to the caller
- Import: if URL is unreachable, return a user-visible error; do not create any records

---

## Section 7: Future Considerations

1. **Recommender agents** — specialized agents that search tool descriptions and recommend tools. Out of scope; separate feature.
2. **Abstractions layer** — introduce `Brigade.Admin.Abstractions` to decouple `Brigade.Agents` from `Brigade.Admin.Data`. Currently both `ISecretsManager` and `IPromptStore` create a data→runtime dependency.
3. **Prompt templates** — parameterized prompts with variable substitution (e.g., `${AGENT_NAME}`). Future enhancement.
4. **Versioning comparisons** — diff view between versions in the history modal.
5. **Bulk operations** — rename paths in bulk, reassign prompts across agents.
6. **Audit log** — formal audit trail (who published, who deleted). Currently covered by `created_by`; a separate `prompt_audit_log` table could be added via migration.

---

## Success Criteria

- [x] Users can create, edit (draft), and publish prompts/skills/tools in tree view
- [x] **Draft versions are mutable; published and archived versions are immutable**
- [x] Agents can select multiple prompts, auto-concatenated in category order then intra-category by assigned `Order`
- [x] New agents automatically include all prompts where `IsDefaultIncluded = true`
- [x] Users can import markdown files from `https://` URLs or paste directly
- [x] Tool whitelist/blacklist correctly filters available tools (whitelist: empty = all allowed; blacklist: takes precedence)
- [x] Publish is atomic — no window where a path has zero published versions
- [x] Database enforces "one published version per path" via filtered unique index
- [x] `CreatedBy` stores subject ID + display name for reliable attribution
- [x] Import endpoint is protected against SSRF and oversized payloads
