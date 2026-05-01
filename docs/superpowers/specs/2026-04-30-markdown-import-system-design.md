# Markdown-Based Prompt, Skill, and Tool Import System — Design Spec

**Date:** 2026-04-30  
**Project:** Brigade (WebDev + Brigade.Agents + Brigade.Admin.Data)  
**Status:** Approved

---

## Overview

Users need a flexible way to manage, organize, and version system prompts, skills, and tool descriptions. This system introduces:

1. **Database-backed prompt library** — all prompts, skills, and tools stored as versioned markdown files
2. **Hierarchical tree organization** — free-form paths (e.g., `/Agents/Prompts/Bash Command`)
3. **Category-based system prompt assembly** — automatic concatenation in the correct order (Foundation → Constraints → TaskSpecific → Overrides)
4. **Draft/publish workflow** — safe editing without breaking live agents
5. **Agent prompt selection UI** — agents explicitly choose which prompts to include
6. **Default inclusion** — prompts can be marked to auto-include in new agents

---

## Section 1: Architecture Overview & Data Model

### Core Entities

**PromptVersion** — immutable, versioned record for prompts, skills, and tool descriptions

Fields:
- `Id` — primary key (GUID)
- `Path` — hierarchical path (e.g., `/Agents/Prompts/Bash Command`). Not null, matches `^/([a-zA-Z0-9_-]+/)*[a-zA-Z0-9_-]+$`
- `Type` — enum: `Prompt`, `Skill`, `ToolDescription`
- `Category` — enum: `Foundation`, `Constraints`, `TaskSpecific`, `Overrides`. Stored per path, not per version.
- `Content` — markdown body (not null)
- `Frontmatter` — optional YAML for Skill and ToolDescription types (markdown comments: `<!-- ... -->`)
- `IsActive` — bool. Only one version per path can be `true`. Enforced by unique constraint `(Path, IsActive = true)`.
- `IsDefaultIncluded` — bool. When `true`, agents auto-include this prompt on creation.
- `CreatedAt` — DateTime (UTC)
- `CreatedBy` — string (username or agent name)
- `Notes` — optional user notes (e.g., "migrated from legacy system", "validated against X")

**Constraints:**
- Unique index: `(Path, IsActive = true)` — ensures only one active version per path
- **Business rule:** Category is tied to Path. All versions of a path must have the same category. If you want to move a prompt to a different category, create a new path with the new category (effectively copying the prompt).
- **Validation:** On creation, verify that if a version already exists for this path, the new version's category matches the existing path's category.

---

**AgentPromptAssignment** — junction table linking agents to selected prompts

Fields:
- `Id` — primary key (GUID)
- `AgentId` — foreign key to Agent/AgentOptions
- `PromptVersionId` — foreign key to PromptVersion (always references active version at runtime)
- `Order` — int. Display order only; does not affect concatenation order (which is determined by Category).

**Constraints:**
- Unique index: `(AgentId, PromptVersionId)`
- Soft delete via `AgentId` cascade (if agent is deleted, its assignments are deleted)

---

**AgentOptions** — updated fields

Additions:
- `SelectedPromptPaths` — `List<string>`. Paths of selected prompts (e.g., `["/Agents/Prompts/Bash Command", "/Rules/Security"]`). Resolved to active versions at agent creation time.
- `ToolWhitelist` — `List<string>`. Tool names allowed (e.g., `["bash", "read-all-lines"]`). If empty/null, all tools are allowed.
- `ToolBlacklist` — `List<string>`. Tool names forbidden (e.g., `["rm", "destroy"]`). Applied after whitelist; blacklist takes precedence.

---

### Data Flow: Agent Instantiation

1. Call `AgentFactory.CreateAgentAsync(agentOptions, cancellationToken)`
2. Resolve secrets (existing logic)
3. **NEW:** Inject `IPromptStore` and fetch prompts:
   - For each path in `SelectedPromptPaths`, get the active `PromptVersion`
   - Sort by `Category` (Foundation, Constraints, TaskSpecific, Overrides)
   - Concatenate `Content` fields into a single markdown string
4. **NEW:** Merge prompt content with inline `Instructions` (inline comes last as override)
5. **NEW:** Apply tool whitelist/blacklist to available tools
6. Create agent with final system prompt and filtered tools

---

## Section 2: Admin UI — Prompt Library Management

**Page:** `/admin/prompts`

**Access:** Admin-only (role-based, integrated with OpenIddict auth)

### Layout

**Left Pane (40%):**
- **Search box** — fuzzy search across `Path`, `Content`, `Frontmatter`
- **Filter toggles:** `[All] [Prompts] [Skills] [Tools]`
- **Hierarchical tree view:**
  - Tree built from `Path` by splitting on `/` (e.g., `/Agents/Prompts/Bash Command` creates folders `Agents` → `Prompts` → `Bash Command`)
  - Expandable folders (no checkbox on folders)
  - Leaf items (prompts/skills/tools) have status icon:
    - 🔵 Active
    - ◯ Draft
    - ✓ Published (not active)
  - Right-click context menu:
    - "New Prompt/Skill/Tool Here" → opens editor with path pre-filled
    - "Rename Path" → if no agents currently depend on this path, allows rename (moves all versions to new path)
    - "Delete" → soft-delete all versions of this path (archived, not shown unless filter enabled)

**Right Pane (60%):**

When no item selected:
- Empty state: "Select a prompt from the tree to view or edit"

When item selected (viewing active version):
- **Header:**
  - Path (read-only)
  - Type (read-only)
  - Category (read-only, but shown for context)
  - Status badge: "Active" or "Draft"
- **Toolbar:**
  - "New Version" button → creates draft based on current active
  - "Edit Draft" button (if draft exists for this path)
  - "View History" link → modal showing all versions, with ability to re-publish old versions
  - "Delete" button → soft-delete all versions
- **Content Display (read-only):**
  - If Skill or ToolDescription: show frontmatter in code block, then markdown content
  - If Prompt: just markdown
- **Draft Editor** (if editing):
  - Markdown editor (syntax highlighting, live preview optional)
  - If Skill or ToolDescription: YAML frontmatter editor above markdown
  - Checkbox: "Mark as default when published"
  - Auto-save to draft (debounced, no explicit save button)
  - "Publish" button → makes draft active, deactivates previous active
  - "Discard Draft" button → deletes draft without publishing

### Import Workflow

**"Import Prompts" button** in toolbar or top of page.

Dialog: "Import from URL"
- Input field: GitHub URL, file URL, or direct markdown text
- Processing (backend API endpoint handles parsing):
  - Fetches markdown files from provided URL (or accepts direct paste)
  - Extracts frontmatter (if present) and content
  - Suggests path based on filename (e.g., `agent-prompt-bash-command.md` → `/Agents/Prompts/Bash Command`)
  - Infers Type from filename prefix: `agent-prompt-*` → Prompt, `skill-*` → Skill, `tool-description-*` → ToolDescription
  - Infers Category from filename or frontmatter (defaulting to TaskSpecific)
  - **Note:** Users can also task an agent with fetching and importing from external sources programmatically
- Preview table:
  - Columns: `Filename`, `Suggested Path`, `Type`, `Category`, `Status`
  - User can edit paths inline before confirming
- Buttons:
  - "Create as Draft" → all versions created as drafts, user reviews and publishes
  - "Create and Publish" → all versions created and marked active immediately

---

## Section 3: Agent Configuration — Prompt Selection

**Integration:** In the agent creation/editing UI (WebDev), add a new section/step: "System Prompts"

### Prompt Selection Modal

Triggered by button "Configure Prompts" or similar in agent editor.

**Layout:**
- Tree view organized by Category (headings, not folders):
  - ▼ **Foundation**
    - ☐ `/System/Project Context` (checked, default)
    - ☐ `/System/Core Identity` (checked, default)
    - ☐ `/Custom/Foundation Example`
  - ▼ **Constraints**
    - ☐ `/Rules/Security` (checked, default)
    - ☐ `/Rules/Git Safety`
  - ▼ **Task-Specific**
    - ☐ `/Agents/Prompts/Bash Command`
    - ☐ `/Agents/Prompts/Code Review`
  - ▼ **Overrides**
    - ☐ `/Custom/Project-Specific Rules`

**Features:**
- Search box to filter by path
- Checkbox per prompt (individual select)
- **Folder checkbox** (if grouped by category/subcategory) → selecting a folder selects all descendants
- Right pane: **Preview of concatenated result** (read-only markdown, showing all selected prompts in order)
- **Status indicators:** Show which prompts are defaults (bold, or icon)

**Buttons:**
- "Reset to Defaults" → re-select all prompts where `IsDefaultIncluded = true`
- "Select All" → select all available prompts
- "Deselect All" → uncheck everything
- "Apply" → saves selection to `AgentOptions.SelectedPromptPaths`

---

## Section 4: Implementation Architecture

### Database Layer

**New service:** `IPromptStore` (interface in `Brigade.Admin.Data`, implementations in PostgreSQL and SQLite projects)

Methods:
```csharp
Task<PromptVersion?> GetActivePromptAsync(string path, CancellationToken ct = default);
Task<PromptVersion?> GetPromptVersionAsync(Guid id, CancellationToken ct = default);
Task<List<PromptVersion>> GetAgentPromptsAsync(Guid agentId, CancellationToken ct = default);
  // Returns active versions for agent, sorted by Category
Task<List<PromptVersion>> GetPromptHistoryAsync(string path, CancellationToken ct = default);
  // Returns all versions of a path, newest first
Task<PromptVersion> CreatePromptVersionAsync(
  string path, string type, string category, string content, 
  string? frontmatter, string createdBy, string? notes, CancellationToken ct = default);
  // Creates new version as draft
Task PublishPromptVersionAsync(Guid versionId, bool markAsDefault = false, CancellationToken ct = default);
  // Marks version as active, deactivates previous
Task DeletePromptAsync(string path, CancellationToken ct = default);
  // Soft-delete all versions of path
Task<List<PromptVersion>> SearchPromptsAsync(
  string query, string? typeFilter = null, CancellationToken ct = default);
  // Fuzzy search by path and content
```

**EF Core configuration:**
- `PromptVersion` mapped to `prompt_versions` table
- `AgentPromptAssignment` mapped to `agent_prompt_assignments` table
- Unique constraint on `(path, is_active = true)`
- Unique constraint on `(agent_id, prompt_version_id)`

---

### WebDev (UI Layer)

**New Razor components:**

1. **`PromptLibrary.razor`** — `/admin/prompts` page
   - Layout: tree + editor (left/right panes)
   - Handles navigation, selection, editing
   - Calls `PromptStore` via HTTP API

2. **`PromptSelector.razor`** — modal/dialog for agent configuration
   - Tree view organized by Category
   - Checkboxes for selection
   - Preview pane
   - Called from agent creation/edit flow

3. **`PromptEditor.razor`** — reusable editor
   - Markdown + frontmatter editing
   - Auto-save to draft
   - Publish button

**New API endpoints:**

- `GET /api/prompts` — list all active prompts (with filter/search)
- `GET /api/prompts/{path}` — get active version of prompt by path
- `GET /api/prompts/{path}/history` — get all versions of a path
- `GET /api/prompts/{id}` — get specific version by ID
- `POST /api/prompts` — create new draft version
- `PUT /api/prompts/{id}` — update draft (auto-save)
- `POST /api/prompts/{id}/publish` — publish draft as active
- `DELETE /api/prompts/{path}` — soft-delete all versions
- `POST /api/prompts/import` — import from URL/markdown (triggers agent task or manual processing)
- `POST /api/agents/{agentId}/prompts` — set selected prompts for agent (bulk update `AgentPromptAssignment`)

---

### Brigade.Agents

**Update `AgentFactory.CreateAgentAsync()`:**

```csharp
public async Task<AIAgent> CreateAgentAsync(AgentOptions options, CancellationToken ct = default)
{
    // Existing secret resolution
    if (secrets.IsSecretReference(options.ApiKey))
    {
        options = options with { ApiKey = await secrets.ResolveAsync(...) };
    }

    // NEW: Fetch and concatenate prompts
    var promptStore = /* injected */;
    var finalInstructions = options.Instructions ?? "";
    
    if (options.SelectedPromptPaths?.Count > 0)
    {
        var prompts = new List<PromptVersion>();
        foreach (var path in options.SelectedPromptPaths)
        {
            var prompt = await promptStore.GetActivePromptAsync(path, ct);
            if (prompt != null) prompts.Add(prompt);
        }
        
        // Sort by Category
        var categorized = prompts
            .GroupBy(p => p.Category)
            .OrderBy(g => (int)g.Key) // Foundation=0, Constraints=1, TaskSpecific=2, Overrides=3
            .SelectMany(g => g);
        
        var concatenated = string.Join("\n\n", categorized.Select(p => p.Content));
        finalInstructions = concatenated + "\n\n" + finalInstructions;
    }

    options = options with { Instructions = finalInstructions };

    // NEW: Apply tool whitelist/blacklist
    var allTools = /* get available tools */;
    var filteredTools = allTools
        .Where(t => (options.ToolWhitelist?.Count == 0 || options.ToolWhitelist.Contains(t)))
        .Where(t => !options.ToolBlacklist?.Contains(t) == true)
        .ToList();

    options = options with { Tools = filteredTools };

    // Existing provider logic
    return Enum.Parse<ProvidersEnum>(options.Provider ?? "UNSUPPORTED") switch { ... };
}
```

**Dependency injection:** Register `IPromptStore` in DI container (PostgreSQL or SQLite implementation based on config)

---

## Section 5: Data Storage & Migration

**New EF Core migration:**

```csharp
// Up
CreateTable("prompt_versions", table => new
{
    id = table.Column<Guid>(nullable: false),
    path = table.Column<string>(maxLength: 500, nullable: false),
    type = table.Column<string>(maxLength: 50, nullable: false), // Prompt, Skill, ToolDescription
    category = table.Column<string>(maxLength: 50, nullable: false), // Foundation, Constraints, ...
    content = table.Column<string>(nullable: false),
    frontmatter = table.Column<string>(nullable: true),
    is_active = table.Column<bool>(nullable: false, defaultValue: false),
    is_default_included = table.Column<bool>(nullable: false, defaultValue: false),
    created_at = table.Column<DateTime>(nullable: false),
    created_by = table.Column<string>(maxLength: 256, nullable: false),
    notes = table.Column<string>(nullable: true),
    PrimaryKey = "pk_prompt_versions"
});

CreateIndex("prompt_versions", new[] { "path", "is_active" }, unique: true, name: "ix_prompt_versions_path_active");

CreateTable("agent_prompt_assignments", table => new
{
    id = table.Column<Guid>(nullable: false),
    agent_id = table.Column<Guid>(nullable: false),
    prompt_version_id = table.Column<Guid>(nullable: false),
    order = table.Column<int>(nullable: false),
    PrimaryKey = "pk_agent_prompt_assignments",
    ForeignKey = ("agent_id", "[AgentTable]", "id"), // TODO: verify Agent table name during implementation
    ForeignKey = ("prompt_version_id", "prompt_versions", "id")
});

CreateIndex("agent_prompt_assignments", new[] { "agent_id", "prompt_version_id" }, unique: true);

// Down: drop both tables
```

---

## Section 6: Error Handling & Validation

**Validation:**
- Path must match regex `^/([a-zA-Z0-9_-]+/)*[a-zA-Z0-9_-]+$`
- Content cannot be empty
- Frontmatter must be valid YAML (for Skill/ToolDescription)
- Only one `is_active = true` per path (enforced by unique constraint)

**Error handling:**
- Import fails if URL is unreachable → user sees error message, can retry
- Publish fails if path was deleted or type changed → user sees message, conflict resolution
- Agent creation fails if selected prompt no longer exists → graceful degradation (use defaults)

---

## Section 7: Future Considerations

1. **Recommender agents** — specialized agents that search tool descriptions and recommend tools. Out of scope for this design; separate feature.
2. **Prompt templates** — parameterized prompts with variable substitution. Future enhancement.
3. **Audit log** — track who published/deleted prompts. Can be added via migration.
4. **Versioning comparisons** — diff view between versions. UI enhancement.
5. **Bulk operations** — rename paths, bulk reassign prompts to agents. Future operations.

---

## Success Criteria

- [x] Users can create, edit (draft), and publish prompts/skills/tools in tree view
- [x] Agents can select multiple prompts, which are auto-concatenated in correct category order
- [x] New agents automatically include default prompts
- [x] Users can import markdown files (from URLs or manual uploads)
- [x] Tool whitelist/blacklist works as filter for agent tools
- [x] Draft/publish workflow prevents accidental changes to live agents
- [x] Database enforces "one active version per path"
- [x] All versions are immutable; edits create new records
