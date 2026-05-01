# YAML-Based Seed Data — Design Spec

**Date:** 2026-04-30
**Project:** Brigade.Admin.Data + WebDev
**Status:** Approved
**Depends on:** `2026-04-30-uuid7-branded-ids-design.md` (IDs in YAML use UUID v7 strings)
**Prerequisite for:** `2026-04-30-markdown-import-system-design.md`

---

## Overview

Seed data is currently hardcoded in static `SeedData.*` partial classes inside `Brigade.Admin.Data`. This replaces that system with YAML files in `src/WebDev/seed/`, loaded and applied at startup by the existing seed services.

**Goals:**
- Seed data is editable without recompiling — change a YAML file, restart, done
- Multiple YAML files can coexist; the loader iterates all `.yml` files in the folder
- Dynamic values (e.g., authority URL) are interpolated from `IConfiguration` via `${ConfigKey}` placeholders
- `SeedData.*` partial classes are deleted after migration

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

Files must be marked **Copy to Output Directory: Always** in `WebDev.csproj` so they are available at runtime:

```xml
<ItemGroup>
  <Content Include="seed\**\*.yml">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

The seed folder path is resolved at runtime using `IWebHostEnvironment.ContentRootPath`:

```csharp
var seedPath = Path.Combine(env.ContentRootPath, "seed");
```

---

## YAML File Format

Each file contains one or more top-level keys identifying the entity type. A single file can contain multiple entity types if convenient, but splitting by type (one file per type) is recommended.

### `agents.yml`

```yaml
agents:
  - name: Default Agent
    description: The default Brigade agent
```

### `users.yml`

```yaml
users:
  - userName: admin
    email: admin@brigade.local
    emailConfirmed: true
    password: Admin123!
```

### `oidc-applications.yml`

```yaml
oidc-applications:
  - clientId: brigade-bff-client-id
    clientSecret: brigade-bff-client-secret
    clientType: confidential
    consentType: implicit
    displayName: Brigade BFF
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
      - scp:brigade-api

  - clientId: brigade-api-client-id
    clientSecret: brigade-api-client-secret
    clientType: confidential
    displayName: Brigade API Test Client
    permissions:
      - ept:token
      - gt:client_credentials
      - scp:brigade-api
```

### `oidc-scopes.yml`

```yaml
oidc-scopes:
  - name: brigade-api
    resources:
      - brigade-webhost
```

### Variable Interpolation

Any string value containing `${ConfigKey}` is replaced at load time using `IConfiguration`:

```csharp
// e.g., "${OpenIddict:Authority}/signin-oidc" → "https://localhost:7000/signin-oidc"
value = Regex.Replace(value, @"\$\{([^}]+)\}", match =>
    configuration[match.Groups[1].Value] ?? match.Value);
```

Interpolation applies recursively to all string values in the deserialized YAML graph.

---

## Supported Entity Types (top-level keys)

| Key | Deserializes to | Seeded by |
|-----|----------------|-----------|
| `agents` | `List<AgentOptions>` | `AdminDbSeedService` |
| `users` | `List<SeedUserDto>` | `AuthDbSeedService` |
| `oidc-applications` | `List<OidcApplicationDto>` | `AuthDbSeedService` |
| `oidc-scopes` | `List<OidcScopeDto>` | `AuthDbSeedService` |

Unknown top-level keys are logged as warnings and skipped.

---

## DTOs

Rather than deserializing directly into `OpenIddictApplicationDescriptor` (which has complex types), simple DTOs map from YAML to the target types.

```csharp
// Brigade.Admin.Data/Seeds/Dtos/SeedUserDto.cs
record SeedUserDto
{
    public string UserName { get; init; } = "";
    public string Email { get; init; } = "";
    public bool EmailConfirmed { get; init; }
    public string Password { get; init; } = "";
}

// Brigade.Admin.Data/Seeds/Dtos/OidcApplicationDto.cs
record OidcApplicationDto
{
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public string ClientType { get; init; } = "confidential";
    public string? ConsentType { get; init; }
    public string? DisplayName { get; init; }
    public List<string> RedirectUris { get; init; } = [];
    public List<string> PostLogoutRedirectUris { get; init; } = [];
    public List<string> Permissions { get; init; } = [];

    public OpenIddictApplicationDescriptor ToDescriptor() => new()
    {
        ClientId = ClientId,
        ClientSecret = ClientSecret,
        ClientType = ClientType,
        ConsentType = ConsentType,
        DisplayName = DisplayName,
        RedirectUris = { RedirectUris.Select(u => new Uri(u)) ... },
        PostLogoutRedirectUris = { PostLogoutRedirectUris.Select(u => new Uri(u)) ... },
        Permissions = { Permissions }
    };
}

// Brigade.Admin.Data/Seeds/Dtos/OidcScopeDto.cs
record OidcScopeDto
{
    public string Name { get; init; } = "";
    public List<string> Resources { get; init; } = [];
}
```

---

## YAML Loader

A new `YamlSeedLoader` class in `Brigade.Admin.Data/Seeds/` handles discovery, parsing, interpolation, and dispatch:

```csharp
// Brigade.Admin.Data/Seeds/YamlSeedLoader.cs
public class YamlSeedLoader(IConfiguration configuration, ILogger<YamlSeedLoader> logger)
{
    public SeedPayload LoadAll(string seedFolderPath)
    {
        var payload = new SeedPayload();
        if (!Directory.Exists(seedFolderPath))
        {
            logger.LogWarning("Seed folder not found: {Path}", seedFolderPath);
            return payload;
        }

        foreach (var file in Directory.EnumerateFiles(seedFolderPath, "*.yml"))
        {
            var yaml = File.ReadAllText(file);
            var interpolated = Interpolate(yaml);
            var document = ParseYaml(interpolated);
            Merge(payload, document, file);
        }

        return payload;
    }

    // ... Interpolate, ParseYaml, Merge
}

public class SeedPayload
{
    public List<AgentOptions> Agents { get; } = [];
    public List<SeedUserDto> Users { get; } = [];
    public List<OidcApplicationDto> OidcApplications { get; } = [];
    public List<OidcScopeDto> OidcScopes { get; } = [];
}
```

---

## Updated Seed Services

### `AdminDbSeedService`

```csharp
public async Task StartAsync(CancellationToken ct)
{
    if (_env != "Development") return;

    var seedPath = Path.Combine(_contentRoot, "seed");
    var payload = _loader.LoadAll(seedPath);

    using var scope = _serviceProvider.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    foreach (var agent in payload.Agents)
    {
        if (!db.Set<AgentOptions>().Any(a => a.Name == agent.Name))
            db.Set<AgentOptions>().Add(agent);
    }

    await db.SaveChangesAsync(ct);
}
```

### `AuthDbSeedService`

Replace all `SeedData.GetDefault*()` calls with `_loader.LoadAll(seedPath)` equivalents. The existing upsert logic for users, applications, and scopes remains unchanged — only the data source changes.

---

## NuGet Dependency

Add `YamlDotNet` to `Brigade.Admin.Data`:

```xml
<PackageReference Include="YamlDotNet" Version="16.*" />
```

---

## Migration Plan for Existing Seed Data

Existing `SeedData.*` classes map to YAML as follows:

| Class | YAML file | Key |
|-------|-----------|-----|
| `SeedData.Agents.cs` | `seed/agents.yml` | `agents` |
| `SeedData.AppUsers.cs` | `seed/users.yml` | `users` |
| `SeedData.Application.cs` | `seed/oidc-applications.yml` | `oidc-applications` |
| `SeedData.Scope.cs` | `seed/oidc-scopes.yml` | `oidc-scopes` |

After YAML files are created and verified, delete:
- `Brigade.Admin.Data/Seeds/SeedData.Agents.cs`
- `Brigade.Admin.Data/Seeds/SeedData.AppUsers.cs`
- `Brigade.Admin.Data/Seeds/SeedData.Application.cs`
- `Brigade.Admin.Data/Seeds/SeedData.Scope.cs`

---

## Files to Create / Modify

**New:**
- `src/WebDev/seed/agents.yml`
- `src/WebDev/seed/users.yml`
- `src/WebDev/seed/oidc-applications.yml`
- `src/WebDev/seed/oidc-scopes.yml`
- `Brigade.Admin.Data/Seeds/YamlSeedLoader.cs`
- `Brigade.Admin.Data/Seeds/SeedPayload.cs`
- `Brigade.Admin.Data/Seeds/Dtos/SeedUserDto.cs`
- `Brigade.Admin.Data/Seeds/Dtos/OidcApplicationDto.cs`
- `Brigade.Admin.Data/Seeds/Dtos/OidcScopeDto.cs`

**Modified:**
- `Brigade.Admin.Data/Services/AdminDbSeedService.cs` — use `YamlSeedLoader`
- `Brigade.Admin.Data/Services/AuthDbSeedService.cs` — use `YamlSeedLoader`
- `WebDev/WebDev.csproj` — add `seed/**/*.yml` copy rule
- `Brigade.Admin.Data/Brigade.Admin.Data.csproj` — add `YamlDotNet` reference

**Deleted:**
- `Brigade.Admin.Data/Seeds/SeedData.Agents.cs`
- `Brigade.Admin.Data/Seeds/SeedData.AppUsers.cs`
- `Brigade.Admin.Data/Seeds/SeedData.Application.cs`
- `Brigade.Admin.Data/Seeds/SeedData.Scope.cs`

---

## Error Handling

- Missing seed folder → log warning, skip (do not crash startup)
- Malformed YAML → log error with filename + line number, skip that file
- Unknown top-level key → log warning, skip that key
- Duplicate entries (e.g., two users with same `userName`) → log warning, use first occurrence
- Failed upsert (e.g., Identity validation error) → log error, continue with next record (do not abort startup)

---

## Success Criteria

- [x] All four existing `SeedData.*` classes are deleted
- [x] YAML files in `seed/` produce identical seeded state to the old `SeedData` classes
- [x] `${ConfigKey}` placeholders in YAML are resolved from `IConfiguration`
- [x] Adding or editing a YAML file and restarting the app applies the new data without recompiling
- [x] Unknown YAML keys are skipped with a warning (no crash)
- [x] Startup does not fail if `seed/` folder is absent
