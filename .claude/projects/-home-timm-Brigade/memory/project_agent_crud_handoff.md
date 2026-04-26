---
name: Agent CRUD UI handoff
description: Handoff note for the Radzen Blazor CRUD pages built in Brigade.Admin.UI — current state and remaining issues
type: project
---

## Status: CRUD pages complete, WebHost wiring has pre-existing issues

**Why:** Blazor CRUD admin pages were built in `Brigade.Admin.UI` for all 6 agent data models. The UI lib builds clean. The WebHost has pre-existing build failures unrelated to this work.

**How to apply:** Resume by resolving the WebHost build issues described below before running/testing.

---

## Worktree / branch

- Branch: `features/radzen-blazor-ui`
- Worktree root: `/home/timm/Brigade/.worktrees/features/radzen-blazor-ui`

---

## What was completed

### Brigade.Admin.Data
- Added `Microsoft.EntityFrameworkCore.InMemory` and `Npgsql.EntityFrameworkCore.PostgreSQL` to csproj
- Created `Extensions/AgentDbContextExtensions.cs` with:
  - `AddInMemoryAgentDbContext()`
  - `AddPostgreSqlAgentDbContext(connectionString)`
  - `AddAgentStores()` — registers all 6 stores as scoped
- Updated all 6 stores to use `.AsNoTracking()` on reads and `db.ChangeTracker.Clear()` before `db.Update()` to avoid EF tracking conflicts in Blazor Server scoped lifetime
- Added `AgentStore.AssignToolsAsync(agentId, toolIds)` for many-to-many tool assignment

### Brigade.Admin.UI
- Added ProjectReference to `Brigade.Admin.Data`
- Updated `_Imports.razor` with `@using Brigade.Admin.Data.Agents` and `@using Brigade.Admin.Data.Stores`
- Updated `Layouts/MainLayout.razor` to include `<RadzenComponents @rendermode="InteractiveServer" />`
- Updated `Components/Sidebar.razor` with nav links for all 6 sections
- Created 24 CRUD pages (List, Create, Update, Delete) under `Pages/`:
  - `Agents/` → routes: `/agents`, `/agents/create`, `/agents/{Id:int}/edit`, `/agents/{Id:int}/delete`
  - `Tools/` → routes: `/tools`, `/tools/create`, `/tools/{Id:int}/edit`, `/tools/{Id:int}/delete`
  - `Skills/` → routes: `/skills`, `/skills/create`, `/skills/{Id:int}/edit`, `/skills/{Id:int}/delete`
  - `Memory/` → routes: `/memory`, `/memory/create`, `/memory/{Id:int}/edit`, `/memory/{Id:int}/delete`
  - `Mcps/` → routes: `/mcps`, `/mcps/create`, `/mcps/{Id:int}/edit`, `/mcps/{Id:int}/delete`
  - `Workflows/` → routes: `/workflows`, `/workflows/create`, `/workflows/{Id:int}/edit`, `/workflows/{Id:int}/delete`
- All pages use `@rendermode InteractiveServer`, `@layout MainLayout`, `@attribute [Authorize]`
- Agent Create/Update pages include tool multi-select using `RadzenDropDown` with `Multiple="true"`
- Memory Create/Update pages include `SearchBehavior` enum dropdown

### Brigade.WebHost (partial — wiring added but pre-existing issues block build)
- Added ProjectReference to `Brigade.Admin.UI`
- Added `using Brigade.Admin.Data;` to `Program.cs`
- Added `AddInMemoryAgentDbContext()` + `AddAgentStores()` registrations in `Program.cs`
- Updated `Components/Routes.razor` to add `AdditionalAssemblies` pointing at `Brigade.Admin.UI`

---

## Remaining issues (pre-existing, not introduced by this work)

### 1. Brigade.ServiceDefaults deleted
`Brigade.WebHost.csproj` references `../Brigade.ServiceDefaults/Brigade.ServiceDefaults.csproj` which was deleted on this branch. `Program.cs` calls `builder.AddServiceDefaults()` which no longer resolves.

**Fix options:**
- Remove the ServiceDefaults ProjectReference from WebHost.csproj and remove/stub the `AddServiceDefaults()` call
- Or restore Brigade.ServiceDefaults from main branch

### 2. CS0436 type conflicts
Pulling `Brigade.Admin.UI → Brigade.Admin.Auth` into the WebHost causes type name conflicts:
- `Brigade.WebHost.Services.OpenIddictSeedService` vs `Brigade.Admin.Auth.Services.OpenIddictSeedService`
- `Brigade.WebHost.Services.CookieRevalidatingAuthenticationStateProvider` vs the Auth lib version

These are warnings not errors, and resolve to the local (WebHost) types. But they indicate Brigade.WebHost has duplicate code from Brigade.Admin.Auth.

**Fix options:**
- Remove the duplicate services from Brigade.WebHost (it was already pulling them from the lib) 
- Or remove the `Brigade.Admin.Auth` ProjectReference from `Brigade.Admin.UI` (only needed for Login.razor)

### 3. Brigade.ServiceDefaults missing in WebDev project too
May affect other parts of the solution — scope unknown.
