# YAML-Based Seed Data — Design Spec

**Date:** 2026-04-30
**Project:** Legion.Admin.Data + WebDev
**Status:** Approved (rev 2 — Opus review applied)
**Depends on:** `2026-04-30-uuid7-branded-ids-design.md`
**Prerequisite for:** `2026-04-30-markdown-import-system-design.md`

---

## Overview

Seed data is currently hardcoded in static `SeedData.*` partial classes inside `Legion.Admin.Data`. This replaces that system with YAML files in `src/WebDev/seed/`, loaded and applied at startup by the existing seed services.

**Goals:**
- Seed data is editable without recompiling — edit source YAML files in `src/WebDev/seed/`, rebuild, restart
- Multiple YAML files can coexist; the loader iterates all `.yml` / `.yaml` files in sorted filename order
- Dynamic values (e.g., authority URL) are interpolated from `IConfiguration` via `${ConfigKey}` placeholders
- Sensitive values (passwords, client secrets) **must** use `${ConfigKey}` — literals are rejected at startup
- `SeedData.*` partial classes are deleted after a one-cycle transition via feature flag

---

## Seed Folder Location

```
src/WebDev/
  seed/
    agents.yml
    users.yml
    oidc-applications.yml
    oidc-scopes.yml
```

Files are marked **Copy to Output Directory: Always** in `WebDev.csproj`:

```xml
<ItemGroup>
  <Content Include="seed\**\*.yml;seed\**\*.yaml">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### Seed Folder Path (Configurable)

The seed folder path is read from configuration, defaulting to `ContentRootPath/seed`. This allows `Legion.WebHost` (the Aspire-hosted app) or any other host to point at its own seed folder without changing library code:

```json
// appsettings.Development.json
{
  "Seeding": {
    "Path": "seed",
    "Source": "Yaml"
  }
}
```

- `Seeding:Path` — relative to `IWebHostEnvironment.ContentRootPath`, or an absolute path. Defaults to `"seed"`.
- `Seeding:Source` — `"Yaml"` (new) or `"Legacy"` (existing `SeedData.*` classes). Used as a one-cycle transition flag; `"Legacy"` support is removed once migration is verified.

---

## YAML File Format

Each file contains one or more top-level keys identifying the entity type. Files are loaded in **sorted filename order** to ensure deterministic application (e.g., scopes before applications, since applications reference scope names).

### `agents.yml`

```yaml
agents:
  - name: Default Agent
    description: The default Legion agent
```

No `id` field. IDs are auto-generated as UUID v7 at insert time if the agent does not already exist (matched by `name`).

### `users.yml`

```yaml
users:
  - userName: admin
    email: admin@legion.local
    emailConfirmed: true
    password: "${Seeding:AdminPassword}"
```

**`password` must use `${ConfigKey}` interpolation.** A literal password value causes a startup error (see Security section). Set the actual value via .NET User Secrets in Development:
```
dotnet user-secrets set "Seeding:AdminPassword" "Admin123!"
```

### `oidc-applications.yml`

```yaml
oidc-applications:
  - clientId: legion-bff-client-id
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

  - clientId: legion-api-client-id
    clientSecret: "${Seeding:ApiClientSecret}"
    clientType: confidential
    displayName: Legion API Test Client
    permissions:
      - ept:token
      - gt:client_credentials
      - scp:legion-api
```

**`clientSecret` must use `${ConfigKey}` interpolation.** Permissions use raw OpenIddict prefix strings (`ept:`, `gt:`, `rt:`, `scp:`). The loader validates each permission against known prefixes at load time and logs a warning on unrecognised values.

### `oidc-scopes.yml`

```yaml
oidc-scopes:
  - name: legion-api
    resources:
      - legion-webhost
```

---

## Variable Interpolation

Interpolation runs **after** YAML is parsed, walking the deserialized object graph and replacing `${ConfigKey}` in every string property. This avoids the risk of config values containing `:` or quotes corrupting the YAML structure before parsing.

```csharp
// Applied recursively after deserialization — not on the raw YAML string
private string Interpolate(string value) =>
    Regex.Replace(value, @"\$\{([^}]+)\}", match =>
        _configuration[match.Groups[1].Value] ?? match.Value);
```

The walker visits all `string` properties on DTOs via reflection (or explicit property mapping), applying `Interpolate` to each. It does not touch non-string types.

---

## Security: Sensitive Field Guard

At load time, after interpolation, the loader checks for unresolved sensitive fields:

```csharp
private static readonly string[] SensitiveFields = ["password", "clientSecret"];

private void GuardSensitiveFields(string fileName, object dto)
{
    foreach (var field in SensitiveFields)
    {
        var prop = dto.GetType().GetProperty(field, BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
        var value = prop?.GetValue(dto) as string;
        if (value is not null && value.StartsWith("${"))
            throw new InvalidOperationException(
                $"Seed file '{fileName}': '{field}' contains an unresolved placeholder '{value}'. " +
                $"Set the config key via User Secrets or environment variables.");
        if (value is not null && !string.IsNullOrEmpty(value) && !value.Contains("${"))
        {
            // After interpolation, if a sensitive field still has a plain literal, warn loudly
            _logger.LogWarning(
                "Seed file '{File}': '{Field}' contains a literal value. " +
                "Use ${{ConfigKey}} and store the value in User Secrets or an environment variable.",
                fileName, field);
        }
    }
}
```

Literal passwords/secrets cause a **startup warning** (not a hard error, to avoid blocking CI). Unresolved `${...}` placeholders cause a **startup exception**.

---

## Supported Entity Types

| Key | DTO | Seeded by |
|-----|-----|-----------|
| `agents` | `SeedAgentDto` | `AdminDbSeedService` |
| `users` | `SeedUserDto` | `AuthDbSeedService` |
| `oidc-applications` | `OidcApplicationDto` | `AuthDbSeedService` |
| `oidc-scopes` | `OidcScopeDto` | `AuthDbSeedService` |

Unknown top-level keys are logged as warnings and skipped (no crash).

---

## DTOs

All entity types use dedicated DTOs for YAML deserialization. This isolates YAML field names from EF Core model shapes and OpenIddict descriptor internals.

```csharp
// Legion.Admin.Data/Seeds/Dtos/SeedAgentDto.cs
record SeedAgentDto
{
    public string Name { get; init; } = "";
    public string? Description { get; init; }
    // Add fields as AgentOptions grows; Id is not sourced from YAML
}

// Legion.Admin.Data/Seeds/Dtos/SeedUserDto.cs
record SeedUserDto
{
    public string UserName { get; init; } = "";
    public string Email { get; init; } = "";
    public bool EmailConfirmed { get; init; }
    public string Password { get; init; } = "";  // must be interpolated
}

// Legion.Admin.Data/Seeds/Dtos/OidcApplicationDto.cs
record OidcApplicationDto
{
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";  // must be interpolated
    public string ClientType { get; init; } = "confidential";
    public string? ConsentType { get; init; }
    public string? DisplayName { get; init; }
    public List<string> RedirectUris { get; init; } = [];
    public List<string> PostLogoutRedirectUris { get; init; } = [];
    public List<string> Permissions { get; init; } = [];

    public OpenIddictApplicationDescriptor ToDescriptor()
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            ClientType = ClientType,
            ConsentType = ConsentType,
            DisplayName = DisplayName,
        };
        foreach (var uri in RedirectUris)
            descriptor.RedirectUris.Add(new Uri(uri));
        foreach (var uri in PostLogoutRedirectUris)
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
        foreach (var permission in Permissions)
            descriptor.Permissions.Add(permission);
        return descriptor;
    }
}

// Legion.Admin.Data/Seeds/Dtos/OidcScopeDto.cs
record OidcScopeDto
{
    public string Name { get; init; } = "";
    public List<string> Resources { get; init; } = [];
}
```

---

## YAML Loader

`YamlSeedLoader` in `Legion.Admin.Data/Seeds/` handles discovery, parsing, interpolation, security checks, and dispatch into `SeedPayload`.

```csharp
public class YamlSeedLoader(IConfiguration configuration, ILogger<YamlSeedLoader> logger)
{
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
            .OrderBy(f => Path.GetFileName(f), StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            try
            {
                var yaml = File.ReadAllText(file);
                var document = Deserialize(yaml);   // parse first, interpolate after
                InterpolateGraph(document);          // walk graph, replace ${...}
                GuardSensitiveFields(file, document);
                Merge(payload, document, file);
            }
            catch (YamlException ex)
            {
                logger.LogError(ex, "Malformed YAML in '{File}' at line {Line} — skipping file",
                    Path.GetFileName(file), ex.Start.Line);
            }
        }

        return payload;
    }

    private static Dictionary<string, object> Deserialize(string yaml)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
        return deserializer.Deserialize<Dictionary<string, object>>(yaml)
            ?? [];
    }

    // ... InterpolateGraph, GuardSensitiveFields, Merge
}

public class SeedPayload
{
    public List<SeedAgentDto> Agents { get; } = [];
    public List<SeedUserDto> Users { get; } = [];
    public List<OidcApplicationDto> OidcApplications { get; } = [];
    public List<OidcScopeDto> OidcScopes { get; } = [];
}
```

**YamlDotNet configuration required:**
- `WithNamingConvention(CamelCaseNamingConvention.Instance)` — YAML uses camelCase (`clientId`), C# properties use PascalCase (`ClientId`)
- `IgnoreUnmatchedProperties()` — unknown YAML keys are silently skipped (errors logged separately)

---

## Updated Seed Services

### `AdminDbSeedService`

```csharp
public async Task StartAsync(CancellationToken ct)
{
    if (_env != "Development") return;
    if (_configuration["Seeding:Source"] == "Legacy") return; // transition flag

    var seedPath = ResolveSeedPath();
    var payload = _loader.LoadAll(seedPath);

    using var scope = _serviceProvider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    foreach (var dto in payload.Agents)
    {
        if (!await db.Set<AgentOptions>().AnyAsync(a => a.Name == dto.Name, ct))
            db.Set<AgentOptions>().Add(new AgentOptions { Name = dto.Name, Description = dto.Description });
    }

    await db.SaveChangesAsync(ct);
}

private string ResolveSeedPath()
{
    var configured = _configuration["Seeding:Path"] ?? "seed";
    return Path.IsPathRooted(configured)
        ? configured
        : Path.Combine(_env.ContentRootPath, configured);
}
```

### `AuthDbSeedService`

The YAML source replaces `SeedData.GetDefault*()` calls. The existing upsert logic is **preserved unchanged** — in particular, the `SeedApplicationsAsync` method continues to read the stored `ClientSecret` from the database before updating, so existing secrets are not overwritten:

```csharp
// Existing behavior preserved — do NOT change this
app.ClientSecret = stored.ClientSecret;
await appManager.PopulateAsync(existing, app, ct);
await appManager.UpdateAsync(existing, ct);
```

User seeding adds an idempotency check that was missing from the original implementation:

```csharp
private async Task SeedUsersAsync(List<SeedUserDto> users, UserManager<ApplicationUser> userManager)
{
    foreach (var dto in users)
    {
        var existing = await userManager.FindByNameAsync(dto.UserName);
        if (existing is not null) continue;  // already seeded — skip

        var user = new ApplicationUser
        {
            UserName = dto.UserName,
            Email = dto.Email,
            EmailConfirmed = dto.EmailConfirmed
        };
        var result = await userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            _logger.LogError("Failed to create user {UserName}: {Errors}",
                dto.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
    }
}
```

---

## NuGet Dependency

Add `YamlDotNet` to `Legion.Admin.Data`:

```xml
<PackageReference Include="YamlDotNet" Version="16.*" />
```

---

## Migration Plan for Existing Seed Data

### Step 1 — Add transition flag

Set `"Seeding:Source": "Yaml"` in `appsettings.Development.json`. Services check this flag; `"Legacy"` falls through to the old `SeedData.*` code path. Both paths remain compilable during transition.

### Step 2 — Create YAML files and User Secrets

Create the four YAML files under `src/WebDev/seed/` as shown above. Add sensitive values to User Secrets:
```
dotnet user-secrets set "Seeding:AdminPassword" "Admin123!"
dotnet user-secrets set "Seeding:BffClientSecret" "legion-bff-client-secret"
dotnet user-secrets set "Seeding:ApiClientSecret" "legion-api-client-secret"
```

### Step 3 — Verify parity

Run both code paths against a clean database and confirm identical seed state.

### Step 4 — Delete legacy code

Remove:
- `Legion.Admin.Data/Seeds/SeedData.Agents.cs`
- `Legion.Admin.Data/Seeds/SeedData.AppUsers.cs`
- `Legion.Admin.Data/Seeds/SeedData.Application.cs`
- `Legion.Admin.Data/Seeds/SeedData.Scope.cs`

Remove the `"Legacy"` branch from both seed services and the `Seeding:Source` flag check.

| Old class | YAML file | Key |
|-----------|-----------|-----|
| `SeedData.Agents.cs` | `seed/agents.yml` | `agents` |
| `SeedData.AppUsers.cs` | `seed/users.yml` | `users` |
| `SeedData.Application.cs` | `seed/oidc-applications.yml` | `oidc-applications` |
| `SeedData.Scope.cs` | `seed/oidc-scopes.yml` | `oidc-scopes` |

---

## Files to Create / Modify

**New:**
- `src/WebDev/seed/agents.yml`
- `src/WebDev/seed/users.yml`
- `src/WebDev/seed/oidc-applications.yml`
- `src/WebDev/seed/oidc-scopes.yml`
- `Legion.Admin.Data/Seeds/YamlSeedLoader.cs`
- `Legion.Admin.Data/Seeds/SeedPayload.cs`
- `Legion.Admin.Data/Seeds/Dtos/SeedAgentDto.cs`
- `Legion.Admin.Data/Seeds/Dtos/SeedUserDto.cs`
- `Legion.Admin.Data/Seeds/Dtos/OidcApplicationDto.cs`
- `Legion.Admin.Data/Seeds/Dtos/OidcScopeDto.cs`

**Modified:**
- `Legion.Admin.Data/Services/AdminDbSeedService.cs` — use `YamlSeedLoader`; add `Seeding:Source` guard; add idempotency
- `Legion.Admin.Data/Services/AuthDbSeedService.cs` — use `YamlSeedLoader`; add `Seeding:Source` guard; add user idempotency
- `WebDev/WebDev.csproj` — copy rule for `seed/**/*.yml` and `*.yaml`
- `Legion.Admin.Data/Legion.Admin.Data.csproj` — add `YamlDotNet` reference
- `WebDev/appsettings.Development.json` — add `Seeding:Path` and `Seeding:Source`

**Deleted (after Step 4):**
- `Legion.Admin.Data/Seeds/SeedData.Agents.cs`
- `Legion.Admin.Data/Seeds/SeedData.AppUsers.cs`
- `Legion.Admin.Data/Seeds/SeedData.Application.cs`
- `Legion.Admin.Data/Seeds/SeedData.Scope.cs`

---

## Error Handling

- Missing seed folder → `LogWarning`, return empty payload (no crash)
- Malformed YAML → `LogError` with filename and `YamlException.Start.Line`, skip that file
- Unknown top-level key → `LogWarning`, skip that key
- Unresolved `${...}` in a sensitive field → throw `InvalidOperationException` at startup
- Literal value in a sensitive field (after interpolation) → `LogWarning`, continue
- Valid permission prefix check → `LogWarning` for unrecognised prefixes, do not reject
- Duplicate entries (same `userName`, same `clientId`) → `LogWarning`, use first occurrence
- Failed upsert → `LogError`, continue with next record (do not abort startup)

---

## Success Criteria

- [x] All four existing `SeedData.*` classes are deleted after verified parity
- [x] YAML files in `seed/` produce identical seeded state to the old `SeedData` classes
- [x] `${ConfigKey}` placeholders in YAML are resolved from `IConfiguration` post-parse
- [x] Sensitive fields (`password`, `clientSecret`) with unresolved placeholders throw at startup
- [x] Sensitive fields with literal values log a warning (not a crash)
- [x] Editing YAML source files and rebuilding/restarting applies the new data
- [x] Unknown YAML keys are skipped with a warning (no crash)
- [x] Startup does not fail if `seed/` folder is absent
- [x] Files are loaded in sorted filename order for determinism
- [x] Both `.yml` and `.yaml` extensions are discovered
- [x] User seeding is idempotent (no errors on second startup)
- [x] Existing `ClientSecret` preservation behavior in `SeedApplicationsAsync` is retained
- [x] Seed folder path is configurable via `Seeding:Path`
- [x] `Seeding:Source = "Legacy"` keeps old code path active during transition
- [x] Unit tests cover: interpolation, sensitive-field guard, unknown key skip, malformed YAML skip, duplicate detection
- [x] Integration test seeds a clean in-memory database and asserts expected entity counts
