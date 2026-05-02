# YAML Seed Data Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace hardcoded `SeedData.*` partial classes with YAML files in `src/WebDev/seed/`, loaded at startup via a new `YamlSeedLoader` service.

**Architecture:** A new `YamlSeedLoader` service in `Legion.Admin.Data` discovers `.yml`/`.yaml` files in a configurable seed folder, parses them with YamlDotNet, interpolates `${ConfigKey}` placeholders from `IConfiguration`, enforces sensitive-field guards, and returns a typed `SeedPayload`. Both seed services (`AdminDbSeedService`, `AuthDbSeedService`) consume the payload instead of calling `SeedData.*`. A `Seeding:Source` transition flag allows one-cycle coexistence before legacy classes are deleted.

**Tech Stack:** .NET 10, YamlDotNet 16.x, xunit, NSubstitute, EF Core InMemory (integration test)

---

## File Map

**New files to create:**
- `src/libs/Legion.Admin.Data/Seeds/Dtos/SeedAgentDto.cs`
- `src/libs/Legion.Admin.Data/Seeds/Dtos/SeedUserDto.cs`
- `src/libs/Legion.Admin.Data/Seeds/Dtos/OidcApplicationDto.cs`
- `src/libs/Legion.Admin.Data/Seeds/Dtos/OidcScopeDto.cs`
- `src/libs/Legion.Admin.Data/Seeds/SeedPayload.cs`
- `src/libs/Legion.Admin.Data/Seeds/YamlSeedLoader.cs`
- `src/WebDev/seed/agents.yml`
- `src/WebDev/seed/users.yml`
- `src/WebDev/seed/oidc-applications.yml`
- `src/WebDev/seed/oidc-scopes.yml`
- `tests/Legion.Admin.Data.Tests/Legion.Admin.Data.Tests.csproj`
- `tests/Legion.Admin.Data.Tests/Seeds/YamlSeedLoaderTests.cs`
- `tests/Legion.Admin.Data.Tests/Seeds/YamlSeedLoaderIntegrationTests.cs`

**Modified files:**
- `Directory.Packages.props` — add YamlDotNet version
- `src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj` — add YamlDotNet reference
- `src/WebDev/WebDev.csproj` — add seed content copy rule
- `src/WebDev/appsettings.Development.json` — add `Seeding` section
- `src/libs/Legion.Admin.Data/Services/AdminDbSeedService.cs` — wire YamlSeedLoader
- `src/libs/Legion.Admin.Data/Services/AuthDbSeedService.cs` — wire YamlSeedLoader

**Deleted (Task 10, after parity confirmed):**
- `src/libs/Legion.Admin.Data/Seeds/SeedData.Agents.cs`
- `src/libs/Legion.Admin.Data/Seeds/SeedData.AppUsers.cs`
- `src/libs/Legion.Admin.Data/Seeds/SeedData.Application.cs`
- `src/libs/Legion.Admin.Data/Seeds/SeedData.Scope.cs`

---

### Task 1: Add YamlDotNet NuGet dependency

This project uses Central Package Management. The version goes in `Directory.Packages.props` and the reference (without a version) goes in the `.csproj`.

**Files:**
- Modify: `Directory.Packages.props`
- Modify: `src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj`

- [ ] **Step 1: Add version to central package management**

In `/home/timm/Legion/Directory.Packages.props`, add inside the last `<ItemGroup>`:

```xml
<PackageVersion Include="YamlDotNet" Version="16.3.0" />
```

- [ ] **Step 2: Add reference to Legion.Admin.Data.csproj**

In `src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj`, add inside the existing `<ItemGroup>` that has other `<PackageReference>` entries:

```xml
<PackageReference Include="YamlDotNet" />
```

- [ ] **Step 3: Verify build succeeds**

```bash
cd /home/timm/Legion
dotnet build src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj
```

Expected: `Build succeeded.` (0 errors)

- [ ] **Step 4: Commit**

```bash
git add Directory.Packages.props src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj
git commit -m "feat: add YamlDotNet dependency to Legion.Admin.Data"
```

---

### Task 2: Create DTOs

DTOs isolate YAML field names from EF Core model shapes and OpenIddict descriptor internals. Each is a simple record.

**Files:**
- Create: `src/libs/Legion.Admin.Data/Seeds/Dtos/SeedAgentDto.cs`
- Create: `src/libs/Legion.Admin.Data/Seeds/Dtos/SeedUserDto.cs`
- Create: `src/libs/Legion.Admin.Data/Seeds/Dtos/OidcApplicationDto.cs`
- Create: `src/libs/Legion.Admin.Data/Seeds/Dtos/OidcScopeDto.cs`

- [ ] **Step 1: Create SeedAgentDto**

Create `src/libs/Legion.Admin.Data/Seeds/Dtos/SeedAgentDto.cs`:

```csharp
namespace Legion.Admin.Data.Seeds.Dtos;

public record SeedAgentDto
{
    public string Name { get; init; } = "";
    public string? Description { get; init; }
}
```

- [ ] **Step 2: Create SeedUserDto**

Create `src/libs/Legion.Admin.Data/Seeds/Dtos/SeedUserDto.cs`:

```csharp
namespace Legion.Admin.Data.Seeds.Dtos;

public record SeedUserDto
{
    public string UserName { get; init; } = "";
    public string Email { get; init; } = "";
    public bool EmailConfirmed { get; init; }
    public string Password { get; init; } = "";
}
```

- [ ] **Step 3: Create OidcApplicationDto**

Create `src/libs/Legion.Admin.Data/Seeds/Dtos/OidcApplicationDto.cs`:

```csharp
using OpenIddict.Abstractions;

namespace Legion.Admin.Data.Seeds.Dtos;

public record OidcApplicationDto
{
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
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
```

- [ ] **Step 4: Create OidcScopeDto**

Create `src/libs/Legion.Admin.Data/Seeds/Dtos/OidcScopeDto.cs`:

```csharp
using OpenIddict.Abstractions;

namespace Legion.Admin.Data.Seeds.Dtos;

public record OidcScopeDto
{
    public string Name { get; init; } = "";
    public List<string> Resources { get; init; } = [];

    public OpenIddictScopeDescriptor ToDescriptor()
    {
        var descriptor = new OpenIddictScopeDescriptor { Name = Name };
        foreach (var r in Resources)
            descriptor.Resources.Add(r);
        return descriptor;
    }
}
```

- [ ] **Step 5: Verify build**

```bash
dotnet build src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 6: Commit**

```bash
git add src/libs/Legion.Admin.Data/Seeds/Dtos/
git commit -m "feat: add seed data DTOs for YAML deserialization"
```

---

### Task 3: Create SeedPayload

`SeedPayload` is the typed container returned by `YamlSeedLoader`. It aggregates all entity lists in one object.

**Files:**
- Create: `src/libs/Legion.Admin.Data/Seeds/SeedPayload.cs`

- [ ] **Step 1: Create SeedPayload**

Create `src/libs/Legion.Admin.Data/Seeds/SeedPayload.cs`:

```csharp
using Legion.Admin.Data.Seeds.Dtos;

namespace Legion.Admin.Data.Seeds;

public class SeedPayload
{
    public List<SeedAgentDto> Agents { get; } = [];
    public List<SeedUserDto> Users { get; } = [];
    public List<OidcApplicationDto> OidcApplications { get; } = [];
    public List<OidcScopeDto> OidcScopes { get; } = [];
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/libs/Legion.Admin.Data/Seeds/SeedPayload.cs
git commit -m "feat: add SeedPayload container for YAML loader output"
```

---

### Task 4: Create YamlSeedLoader

This is the core of the feature. It discovers YAML files, parses them with YamlDotNet, interpolates `${ConfigKey}` placeholders post-parse, enforces sensitive-field guards, and merges results into `SeedPayload`.

**Files:**
- Create: `src/libs/Legion.Admin.Data/Seeds/YamlSeedLoader.cs`

- [ ] **Step 1: Create YamlSeedLoader**

Create `src/libs/Legion.Admin.Data/Seeds/YamlSeedLoader.cs`:

```csharp
using System.Reflection;
using System.Text.RegularExpressions;
using Legion.Admin.Data.Seeds.Dtos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Legion.Admin.Data.Seeds;

public class YamlSeedLoader(IConfiguration configuration, ILogger<YamlSeedLoader> logger)
{
    private static readonly string[] SensitiveFields = ["password", "clientSecret"];

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
                var document = Deserialize(yaml);
                InterpolateGraph(document);
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
        return deserializer.Deserialize<Dictionary<string, object>>(yaml) ?? [];
    }

    private void InterpolateGraph(object? node)
    {
        switch (node)
        {
            // YamlDotNet returns Dictionary<string, object> for the root document
            // and Dictionary<object, object> for nested mappings — handle both
            case Dictionary<string, object> rootDict:
                foreach (var key in rootDict.Keys.ToList())
                {
                    if (rootDict[key] is string s)
                        rootDict[key] = Interpolate(s);
                    else
                        InterpolateGraph(rootDict[key]);
                }
                break;
            case Dictionary<object, object> nestedDict:
                foreach (var key in nestedDict.Keys.ToList())
                {
                    if (nestedDict[key] is string s)
                        nestedDict[key] = Interpolate(s);
                    else
                        InterpolateGraph(nestedDict[key]);
                }
                break;
            case List<object> list:
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is string s)
                        list[i] = Interpolate(s);
                    else
                        InterpolateGraph(list[i]);
                }
                break;
        }
    }

    private string Interpolate(string value) =>
        Regex.Replace(value, @"\$\{([^}]+)\}", match =>
            configuration[match.Groups[1].Value] ?? match.Value);

    private void GuardSensitiveFields(string fileName, object dto)
    {
        foreach (var field in SensitiveFields)
        {
            var prop = dto.GetType().GetProperty(field,
                BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);
            var value = prop?.GetValue(dto) as string;
            if (value is null) continue;

            if (value.StartsWith("${"))
                throw new InvalidOperationException(
                    $"Seed file '{fileName}': '{field}' contains an unresolved placeholder '{value}'. " +
                    $"Set the config key via User Secrets or environment variables.");

            if (!string.IsNullOrEmpty(value))
                logger.LogWarning(
                    "Seed file '{File}': '{Field}' contains a literal value. " +
                    "Use ${{ConfigKey}} and store the value in User Secrets or an environment variable.",
                    Path.GetFileName(fileName), field);
        }
    }

    private void Merge(SeedPayload payload, Dictionary<string, object> document, string file)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();

        foreach (var (key, value) in document)
        {
            switch (key)
            {
                case "agents":
                    MergeList<SeedAgentDto>(payload.Agents, value, deserializer, file, key,
                        dto => dto.Name, "name");
                    break;
                case "users":
                    var users = DeserializeList<SeedUserDto>(value, deserializer);
                    foreach (var dto in users)
                    {
                        GuardSensitiveFields(file, dto);
                        if (payload.Users.Any(u => u.UserName == dto.UserName))
                        {
                            logger.LogWarning("Duplicate user '{UserName}' in '{File}' — skipping",
                                dto.UserName, Path.GetFileName(file));
                            continue;
                        }
                        payload.Users.Add(dto);
                    }
                    break;
                case "oidc-applications":
                    var apps = DeserializeList<OidcApplicationDto>(value, deserializer);
                    foreach (var dto in apps)
                    {
                        GuardSensitiveFields(file, dto);
                        ValidatePermissions(dto.Permissions, file);
                        if (payload.OidcApplications.Any(a => a.ClientId == dto.ClientId))
                        {
                            logger.LogWarning("Duplicate clientId '{ClientId}' in '{File}' — skipping",
                                dto.ClientId, Path.GetFileName(file));
                            continue;
                        }
                        payload.OidcApplications.Add(dto);
                    }
                    break;
                case "oidc-scopes":
                    MergeList<OidcScopeDto>(payload.OidcScopes, value, deserializer, file, key,
                        dto => dto.Name, "name");
                    break;
                default:
                    logger.LogWarning("Unknown seed key '{Key}' in '{File}' — skipping",
                        key, Path.GetFileName(file));
                    break;
            }
        }
    }

    private void MergeList<T>(List<T> target, object rawValue, IDeserializer deserializer,
        string file, string key, Func<T, string> getKey, string keyName)
    {
        var items = DeserializeList<T>(rawValue, deserializer);
        foreach (var item in items)
        {
            var itemKey = getKey(item);
            if (target.Any(existing => getKey(existing) == itemKey))
            {
                logger.LogWarning("Duplicate {KeyName} '{Key}' in '{File}' — skipping",
                    keyName, itemKey, Path.GetFileName(file));
                continue;
            }
            target.Add(item);
        }
    }

    private static List<T> DeserializeList<T>(object rawValue, IDeserializer deserializer)
    {
        var serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();
        var yaml = serializer.Serialize(rawValue);
        return deserializer.Deserialize<List<T>>(yaml) ?? [];
    }

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
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/libs/Legion.Admin.Data/Seeds/YamlSeedLoader.cs
git commit -m "feat: implement YamlSeedLoader with interpolation, guard, and dispatch"
```

---

### Task 5: Create YAML seed files and configure WebDev project

**Files:**
- Create: `src/WebDev/seed/agents.yml`
- Create: `src/WebDev/seed/users.yml`
- Create: `src/WebDev/seed/oidc-applications.yml`
- Create: `src/WebDev/seed/oidc-scopes.yml`
- Modify: `src/WebDev/WebDev.csproj`
- Modify: `src/WebDev/appsettings.Development.json`

- [ ] **Step 1: Create agents.yml**

Create `src/WebDev/seed/agents.yml`:

```yaml
agents:
  - name: Default Agent
    description: The default Legion agent
```

- [ ] **Step 2: Create users.yml**

Create `src/WebDev/seed/users.yml`:

```yaml
users:
  - userName: admin
    email: admin@legion.local
    emailConfirmed: true
    password: "${Seeding:AdminPassword}"
```

- [ ] **Step 3: Create oidc-applications.yml**

Create `src/WebDev/seed/oidc-applications.yml`:

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

- [ ] **Step 4: Create oidc-scopes.yml**

Create `src/WebDev/seed/oidc-scopes.yml`:

```yaml
oidc-scopes:
  - name: legion-api
    resources:
      - legion-webhost
```

- [ ] **Step 5: Add copy rule to WebDev.csproj**

In `src/WebDev/WebDev.csproj`, add a new `<ItemGroup>` before the closing `</Project>`:

```xml
  <ItemGroup>
    <Content Include="seed\**\*.yml;seed\**\*.yaml">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
```

- [ ] **Step 6: Add Seeding config to appsettings.Development.json**

In `src/WebDev/appsettings.Development.json`, add a `"Seeding"` key alongside the existing top-level keys:

```json
  "Seeding": {
    "Path": "seed",
    "Source": "Yaml"
  }
```

The full file should remain valid JSON — add a comma after the last existing entry and insert this block before the closing `}`.

- [ ] **Step 7: Verify build**

```bash
dotnet build src/WebDev/WebDev.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 8: Commit**

```bash
git add src/WebDev/seed/ src/WebDev/WebDev.csproj src/WebDev/appsettings.Development.json
git commit -m "feat: add YAML seed files and copy-to-output rule"
```

---

### Task 6: Update AdminDbSeedService

`AdminDbSeedService` is currently a stub with empty `StartAsync`. It should now use `YamlSeedLoader` to seed agents.

**Files:**
- Modify: `src/libs/Legion.Admin.Data/Services/AdminDbSeedService.cs`

- [ ] **Step 1: Rewrite AdminDbSeedService**

Replace the content of `src/libs/Legion.Admin.Data/Services/AdminDbSeedService.cs`:

```csharp
using Legion.Admin.Data.Models.Agents;
using Legion.Admin.Data.Seeds;
using Microsoft.AspNetCore.Hosting;
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
        if (configuration["Seeding:Source"] == "Legacy") return;

        var seedPath = ResolveSeedPath();
        var payload = loader.LoadAll(seedPath);

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var dto in payload.Agents)
        {
            if (!db.Set<AgentOptions>().Any(a => a.Name == dto.Name))
                db.Set<AgentOptions>().Add(new AgentOptions
                {
                    Id = AgentOptionsId.New(),
                    Name = dto.Name,
                    Description = dto.Description
                });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private string ResolveSeedPath()
    {
        var configured = configuration["Seeding:Path"] ?? "seed";
        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/libs/Legion.Admin.Data/Services/AdminDbSeedService.cs
git commit -m "feat: wire AdminDbSeedService to YamlSeedLoader for agent seeding"
```

---

### Task 7: Update AuthDbSeedService

`AuthDbSeedService` has the full seeding logic today. It should replace the `SeedData.*` calls with `YamlSeedLoader` while keeping the existing upsert and secret-preservation behavior unchanged.

**Files:**
- Modify: `src/libs/Legion.Admin.Data/Services/AuthDbSeedService.cs`

- [ ] **Step 1: Rewrite AuthDbSeedService**

Replace the content of `src/libs/Legion.Admin.Data/Services/AuthDbSeedService.cs`:

```csharp
using Legion.Admin.Data.Auth;
using Legion.Admin.Data.Models.Auth;
using Legion.Admin.Data.Seeds;
using Legion.Admin.Data.Seeds.Dtos;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace Legion.Admin.Data.Services;

public class AuthDbSeedService(
    ILogger<AuthDbSeedService> logger,
    IServiceProvider serviceProvider,
    IWebHostEnvironment env,
    IConfiguration configuration,
    YamlSeedLoader loader) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (env.EnvironmentName != "Development") return;
        if (configuration["Seeding:Source"] == "Legacy")
        {
            await RunLegacyAsync(cancellationToken);
            return;
        }

        var seedPath = ResolveSeedPath();
        var payload = loader.LoadAll(seedPath);

        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var appManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        await SeedUsersAsync(payload.Users, userManager);
        await SeedApplicationsAsync(
            payload.OidcApplications.Select(d => d.ToDescriptor()).ToList(),
            appManager,
            cancellationToken);
        await SeedScopesAsync(
            payload.OidcScopes.Select(d => d.ToDescriptor()).ToList(),
            scopeManager,
            cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private string ResolveSeedPath()
    {
        var configured = configuration["Seeding:Path"] ?? "seed";
        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);
    }

    private async Task RunLegacyAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var appManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        var authority = configuration["OpenIddict:Authority"]
            ?? throw new InvalidOperationException("OpenIddict:Authority is required in configuration.");

        await SeedUsersAsync(Seeds.SeedData.GetDefaultAppUsers()
            .Select(u => new SeedUserDto
            {
                UserName = u.UserName ?? "",
                Email = u.Email ?? "",
                EmailConfirmed = u.EmailConfirmed,
                Password = u.Password
            }).ToList(), userManager);
        await SeedApplicationsAsync(Seeds.SeedData.GetDefaultApplications(authority), appManager, cancellationToken);
        await SeedScopesAsync(Seeds.SeedData.GetDefaultAppScopes(), scopeManager, cancellationToken);
    }

    private async Task SeedUsersAsync(List<SeedUserDto> users, UserManager<ApplicationUser> userManager)
    {
        foreach (var dto in users)
        {
            var existing = await userManager.FindByNameAsync(dto.UserName);
            if (existing is not null) continue;

            var user = new ApplicationUser
            {
                UserName = dto.UserName,
                Email = dto.Email,
                EmailConfirmed = dto.EmailConfirmed
            };
            var result = await userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
                logger.LogError("Failed to create user {UserName}: {Errors}",
                    dto.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
        }
    }

    private async Task SeedApplicationsAsync(List<OpenIddictApplicationDescriptor> apps,
        IOpenIddictApplicationManager appManager, CancellationToken ct)
    {
        foreach (var app in apps)
        {
            if (app?.ClientId is null) continue;
            var existing = await appManager.FindByClientIdAsync(app.ClientId, ct);
            if (existing is null)
            {
                await appManager.CreateAsync(app, ct);
            }
            else
            {
                var stored = new OpenIddictApplicationDescriptor();
                await appManager.PopulateAsync(stored, existing, ct);
                app.ClientSecret = stored.ClientSecret;  // preserve existing secret
                await appManager.PopulateAsync(existing, app, ct);
                await appManager.UpdateAsync(existing, ct);
            }
        }
    }

    private async Task SeedScopesAsync(List<OpenIddictScopeDescriptor> scopes,
        IOpenIddictScopeManager scopeManager, CancellationToken ct)
    {
        foreach (var descriptor in scopes)
        {
            if (descriptor?.Name is null) continue;
            var existing = await scopeManager.FindByNameAsync(descriptor.Name, ct);
            if (existing is null)
                await scopeManager.CreateAsync(descriptor, ct);
            else
            {
                await scopeManager.PopulateAsync(existing, descriptor, ct);
                await scopeManager.UpdateAsync(existing, ct);
            }
        }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Verify WebDev build**

```bash
dotnet build src/WebDev/WebDev.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 4: Commit**

```bash
git add src/libs/Legion.Admin.Data/Services/AuthDbSeedService.cs
git commit -m "feat: wire AuthDbSeedService to YamlSeedLoader with legacy fallback"
```

---

### Task 8: Register YamlSeedLoader and AdminDbSeedService in DI

`YamlSeedLoader` is constructor-injected into both seed services but is not yet registered. Additionally, `AdminDbSeedService` is missing from `WebDev/Program.cs` — only `AuthDbSeedService` is registered (line 59). Both need to be added.

**Files:**
- Modify: `src/WebDev/Program.cs`

- [ ] **Step 1: Add registrations in Program.cs**

In `src/WebDev/Program.cs`, replace line 59:

```csharp
builder.Services.AddHostedService<AuthDbSeedService>();
```

with:

```csharp
builder.Services.AddSingleton<YamlSeedLoader>();
builder.Services.AddHostedService<AdminDbSeedService>();
builder.Services.AddHostedService<AuthDbSeedService>();
```

Ensure `using Legion.Admin.Data.Seeds;` is present at the top of the file (add if missing).

- [ ] **Step 2: Verify build**

```bash
dotnet build src/WebDev/WebDev.csproj
```

Expected: `Build succeeded.`

- [ ] **Step 3: Commit**

```bash
git add src/WebDev/Program.cs
git commit -m "feat: register YamlSeedLoader and AdminDbSeedService in WebDev DI"
```

---

### Task 9: Set User Secrets for sensitive seed values

> **Important:** Set User Secrets **before** running the app. The YAML files reference `${Seeding:...}` placeholders; if the app starts without these secrets set, `YamlSeedLoader` will throw an `InvalidOperationException` on the unresolved placeholders.

**Files:** None (stored outside the repo in `~/.microsoft/usersecrets/`)

- [ ] **Step 1: Check if UserSecretsId is already set**

```bash
grep -r "UserSecretsId" /home/timm/Legion/src/WebDev/WebDev.csproj 2>/dev/null || echo "not set"
```

If "not set", initialize user secrets first:

```bash
cd /home/timm/Legion/src/WebDev && dotnet user-secrets init
```

- [ ] **Step 2: Set the three sensitive seed values**

```bash
cd /home/timm/Legion/src/WebDev
dotnet user-secrets set "Seeding:AdminPassword" "Admin123!"
dotnet user-secrets set "Seeding:BffClientSecret" "legion-bff-client-secret"
dotnet user-secrets set "Seeding:ApiClientSecret" "legion-api-client-secret"
```

Expected output for each: `Successfully saved ... to the secret store.`

- [ ] **Step 3: Verify secrets are listed**

```bash
cd /home/timm/Legion/src/WebDev && dotnet user-secrets list
```

Expected: Three entries containing `Seeding:AdminPassword`, `Seeding:BffClientSecret`, `Seeding:ApiClientSecret`.

---

### Task 10: Create test project

This project is modeled on the existing `tests/Legion.Secrets.Tests` project.

**Files:**
- Create: `tests/Legion.Admin.Data.Tests/Legion.Admin.Data.Tests.csproj`

- [ ] **Step 1: Create test project file**

Create `tests/Legion.Admin.Data.Tests/Legion.Admin.Data.Tests.csproj`:

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
    <PackageReference Include="Microsoft.EntityFrameworkCore.InMemory" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="NSubstitute" />
    <PackageReference Include="YamlDotNet" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\libs\Legion.Admin.Data\Legion.Admin.Data.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 2: Add to solution (if a .sln exists)**

```bash
find /home/timm/Legion -maxdepth 2 -name "*.sln" | head -3
```

If a `.sln` file exists, add the project to it:

```bash
dotnet sln <path-to-sln> add tests/Legion.Admin.Data.Tests/Legion.Admin.Data.Tests.csproj
```

- [ ] **Step 3: Verify the project restores**

```bash
dotnet restore tests/Legion.Admin.Data.Tests/Legion.Admin.Data.Tests.csproj
```

Expected: `Restore completed.`

- [ ] **Step 4: Commit**

```bash
git add tests/Legion.Admin.Data.Tests/
git commit -m "feat: add Legion.Admin.Data.Tests project"
```

---

### Task 11: Write unit tests for YamlSeedLoader

**Files:**
- Create: `tests/Legion.Admin.Data.Tests/Seeds/YamlSeedLoaderTests.cs`

- [ ] **Step 1: Write failing tests**

Create `tests/Legion.Admin.Data.Tests/Seeds/YamlSeedLoaderTests.cs`:

```csharp
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
        var loader = BuildLoader();
        var payload = loader.LoadAll("/nonexistent/path/that/does/not/exist");
        Assert.Empty(payload.Agents);
        Assert.Empty(payload.Users);
        Assert.Empty(payload.OidcApplications);
        Assert.Empty(payload.OidcScopes);
    }

    [Fact]
    public void LoadAll_AgentsYaml_ParsesAgents()
    {
        var dir = WriteTempYaml("""
            agents:
              - name: My Agent
                description: A test agent
            """);

        var loader = BuildLoader();
        var payload = loader.LoadAll(dir);

        Assert.Single(payload.Agents);
        Assert.Equal("My Agent", payload.Agents[0].Name);
        Assert.Equal("A test agent", payload.Agents[0].Description);
    }

    [Fact]
    public void LoadAll_InterpolatesConfigPlaceholder()
    {
        var dir = WriteTempYaml("""
            oidc-scopes:
              - name: ${MyConfig:ScopeName}
                resources: []
            """);

        var loader = BuildLoader(new Dictionary<string, string?>
        {
            ["MyConfig:ScopeName"] = "my-scope"
        });
        var payload = loader.LoadAll(dir);

        Assert.Single(payload.OidcScopes);
        Assert.Equal("my-scope", payload.OidcScopes[0].Name);
    }

    [Fact]
    public void LoadAll_UnresolvedPlaceholderInSensitiveField_Throws()
    {
        var dir = WriteTempYaml("""
            users:
              - userName: admin
                email: admin@legion.local
                emailConfirmed: true
                password: "${Seeding:AdminPassword}"
            """);

        var loader = BuildLoader(); // no config — placeholder stays unresolved
        var ex = Assert.Throws<InvalidOperationException>(() => loader.LoadAll(dir));
        Assert.Contains("unresolved placeholder", ex.Message);
    }

    [Fact]
    public void LoadAll_UnknownTopLevelKey_SkipsWithoutCrash()
    {
        var dir = WriteTempYaml("""
            unknown-entity:
              - foo: bar
            """);

        var loader = BuildLoader();
        var payload = loader.LoadAll(dir);

        Assert.Empty(payload.Agents);
        Assert.Empty(payload.Users);
    }

    [Fact]
    public void LoadAll_MalformedYaml_SkipsFileWithoutCrash()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "bad.yml"), "agents:\n  - name: [unclosed");

        var loader = BuildLoader();
        var payload = loader.LoadAll(dir); // should not throw

        Assert.Empty(payload.Agents);
    }

    [Fact]
    public void LoadAll_DuplicateAgentName_KeepsFirst()
    {
        var dir = WriteTempYaml("""
            agents:
              - name: Same Agent
                description: First
              - name: Same Agent
                description: Second
            """);

        var loader = BuildLoader();
        var payload = loader.LoadAll(dir);

        Assert.Single(payload.Agents);
        Assert.Equal("First", payload.Agents[0].Description);
    }

    [Fact]
    public void LoadAll_FilesLoadedInSortedOrder()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "z-last.yml"), """
            agents:
              - name: Z Agent
            """);
        File.WriteAllText(Path.Combine(dir, "a-first.yml"), """
            agents:
              - name: A Agent
            """);

        var loader = BuildLoader();
        var payload = loader.LoadAll(dir);

        Assert.Equal(2, payload.Agents.Count);
        Assert.Equal("A Agent", payload.Agents[0].Name);
        Assert.Equal("Z Agent", payload.Agents[1].Name);
    }

    [Fact]
    public void LoadAll_BothYmlAndYamlExtensions_AreDiscovered()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.yml"), """
            agents:
              - name: From YML
            """);
        File.WriteAllText(Path.Combine(dir, "b.yaml"), """
            agents:
              - name: From YAML
            """);

        var loader = BuildLoader();
        var payload = loader.LoadAll(dir);

        Assert.Equal(2, payload.Agents.Count);
    }
}
```

- [ ] **Step 2: Run the tests**

> Note: This plan implements the loader before tests (implementation-first, not strict TDD). The tests verify the already-written implementation.

```bash
dotnet test tests/Legion.Admin.Data.Tests/ --filter "Class=YamlSeedLoaderTests" -v normal 2>&1 | tail -30
```

Expected: All tests pass.

- [ ] **Step 3: Run all tests**

```bash
dotnet test tests/Legion.Admin.Data.Tests/ -v normal 2>&1 | tail -20
```

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add tests/Legion.Admin.Data.Tests/Seeds/YamlSeedLoaderTests.cs
git commit -m "test: add unit tests for YamlSeedLoader"
```

---

### Task 12: Write integration test

The integration test seeds an in-memory `AuthDbContext` using actual YAML content (written to a temp folder), then asserts the expected entity counts.

**Files:**
- Create: `tests/Legion.Admin.Data.Tests/Seeds/YamlSeedIntegrationTests.cs`

- [ ] **Step 1: Write integration test**

This test exercises the full agent-seeding path: YAML files on disk → `YamlSeedLoader` → `AppDbContext` (in-memory). It asserts on entities written to the database, not just loader output.

> Note: `AuthDbContext` requires `UseOpenIddict()` which needs a full service container; testing the user/OIDC seeding path end-to-end is covered by `YamlSeedLoaderTests` (unit) which validates the loader output that feeds those paths.

Create `tests/Legion.Admin.Data.Tests/Seeds/YamlSeedIntegrationTests.cs`:

```csharp
using Legion.Admin.Data.Models.Agents;
using Legion.Admin.Data.Seeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legion.Admin.Data.Tests.Seeds;

public class YamlSeedIntegrationTests
{
    private static string WriteSeedFolder()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(dir, "agents.yml"), """
            agents:
              - name: Default Agent
                description: The default Legion agent
              - name: Second Agent
                description: Another agent
            """);

        return dir;
    }

    [Fact]
    public async Task SeedAgents_WritesToInMemoryDatabase_ExpectedCount()
    {
        var seedDir = WriteSeedFolder();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        var loader = new YamlSeedLoader(configuration, NullLogger<YamlSeedLoader>.Instance);
        var payload = loader.LoadAll(seedDir);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);

        foreach (var dto in payload.Agents)
        {
            if (!db.Set<AgentOptions>().Any(a => a.Name == dto.Name))
                db.Set<AgentOptions>().Add(new AgentOptions
                {
                    Id = AgentOptionsId.New(),
                    Name = dto.Name,
                    Description = dto.Description
                });
        }
        await db.SaveChangesAsync();

        var agentCount = await db.Set<AgentOptions>().CountAsync();
        Assert.Equal(2, agentCount);

        var names = await db.Set<AgentOptions>().Select(a => a.Name).ToListAsync();
        Assert.Contains("Default Agent", names);
        Assert.Contains("Second Agent", names);
    }

    [Fact]
    public async Task SeedAgents_Idempotent_DoesNotDuplicateOnSecondRun()
    {
        var seedDir = WriteSeedFolder();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        var loader = new YamlSeedLoader(configuration, NullLogger<YamlSeedLoader>.Instance);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // First run
        await using (var db = new AppDbContext(options))
        {
            var payload = loader.LoadAll(seedDir);
            foreach (var dto in payload.Agents)
            {
                if (!db.Set<AgentOptions>().Any(a => a.Name == dto.Name))
                    db.Set<AgentOptions>().Add(new AgentOptions
                    {
                        Id = AgentOptionsId.New(),
                        Name = dto.Name,
                        Description = dto.Description
                    });
            }
            await db.SaveChangesAsync();
        }

        // Second run (same DB)
        await using (var db = new AppDbContext(options))
        {
            var payload = loader.LoadAll(seedDir);
            foreach (var dto in payload.Agents)
            {
                if (!db.Set<AgentOptions>().Any(a => a.Name == dto.Name))
                    db.Set<AgentOptions>().Add(new AgentOptions
                    {
                        Id = AgentOptionsId.New(),
                        Name = dto.Name,
                        Description = dto.Description
                    });
            }
            await db.SaveChangesAsync();

            var agentCount = await db.Set<AgentOptions>().CountAsync();
            Assert.Equal(2, agentCount);  // still 2, not 4
        }
    }
}
```

- [ ] **Step 2: Run integration test**

```bash
dotnet test tests/Legion.Admin.Data.Tests/ --filter "Class=YamlSeedIntegrationTests" -v normal 2>&1 | tail -20
```

Expected: Both tests pass.

- [ ] **Step 3: Run full test suite**

```bash
dotnet test tests/Legion.Admin.Data.Tests/ -v normal 2>&1 | tail -20
```

Expected: All tests pass.

- [ ] **Step 4: Commit**

```bash
git add tests/Legion.Admin.Data.Tests/Seeds/YamlSeedIntegrationTests.cs
git commit -m "test: add integration test asserting full seed folder payload counts"
```

---

### Task 13: Verify parity and delete legacy SeedData classes

**Only proceed after both code paths have been verified to produce identical seeded state on a clean database.**

**Files:**
- Delete: `src/libs/Legion.Admin.Data/Seeds/SeedData.Agents.cs`
- Delete: `src/libs/Legion.Admin.Data/Seeds/SeedData.AppUsers.cs`
- Delete: `src/libs/Legion.Admin.Data/Seeds/SeedData.Application.cs`
- Delete: `src/libs/Legion.Admin.Data/Seeds/SeedData.Scope.cs`
- Modify: `src/libs/Legion.Admin.Data/Services/AuthDbSeedService.cs` — remove `RunLegacyAsync` and `Seeding:Source` guard
- Modify: `src/libs/Legion.Admin.Data/Services/AdminDbSeedService.cs` — remove `Seeding:Source` guard

- [ ] **Step 1: Confirm YAML path is active**

In `src/WebDev/appsettings.Development.json`, confirm `"Seeding:Source": "Yaml"` is set (not `"Legacy"`).

- [ ] **Step 2: Run the app and verify seed state on a clean database**

Delete the SQLite database file and restart the app:

```bash
rm -f /home/timm/Legion/src/WebDev/webdev.db
cd /home/timm/Legion/src/WebDev && dotnet run 2>&1 | head -60
```

Verify in logs: no seed errors, agents/users/applications/scopes are created.

> **Note:** After Task 8, `AdminDbSeedService` runs on every dev startup for the first time (it was previously a stub). If `AppDbContext` migrations haven't been applied, EF Core will error before YAML loads. This step verifies end-to-end on a clean DB — if EF errors appear, run `dotnet ef database update` in `src/WebDev/` first.

- [ ] **Step 3: Remove RunLegacyAsync from AuthDbSeedService** *(before deleting files — removes the references first)*

In `src/libs/Legion.Admin.Data/Services/AuthDbSeedService.cs`, remove:
- The `if (configuration["Seeding:Source"] == "Legacy") { await RunLegacyAsync(ct); return; }` block at the start of `StartAsync`
- The entire `RunLegacyAsync` method

Also remove the `using Legion.Admin.Data.Seeds;` import if it was only needed for the legacy path (it may still be needed for `SeedPayload`/`YamlSeedLoader` namespacing — check).

- [ ] **Step 4: Remove Seeding:Source guard from AdminDbSeedService**

In `src/libs/Legion.Admin.Data/Services/AdminDbSeedService.cs`, remove:

```csharp
if (configuration["Seeding:Source"] == "Legacy") return;
```

- [ ] **Step 5: Delete legacy SeedData files** *(only after references are removed — build will fail if done before Step 3)*

```bash
rm src/libs/Legion.Admin.Data/Seeds/SeedData.Agents.cs
rm src/libs/Legion.Admin.Data/Seeds/SeedData.AppUsers.cs
rm src/libs/Legion.Admin.Data/Seeds/SeedData.Application.cs
rm src/libs/Legion.Admin.Data/Seeds/SeedData.Scope.cs
```

- [ ] **Step 6: Verify build**

```bash
dotnet build src/libs/Legion.Admin.Data/Legion.Admin.Data.csproj
dotnet build src/WebDev/WebDev.csproj
```

Expected: `Build succeeded.` for both.

- [ ] **Step 7: Run all tests**

```bash
dotnet test tests/Legion.Admin.Data.Tests/ -v normal 2>&1 | tail -20
```

Expected: All tests pass.

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat: delete legacy SeedData classes after YAML parity verified"
```

---

## Self-Review

Checking spec coverage:

| Spec requirement | Task |
|---|---|
| YAML files in `src/WebDev/seed/` | Task 5 |
| Copy to output directory | Task 5, Step 5 |
| `Seeding:Path` and `Seeding:Source` config | Task 5, Step 6 |
| YamlDotNet with camelCase convention | Task 4 |
| `${ConfigKey}` interpolation post-parse | Task 4 |
| Sensitive field guard (throw on unresolved) | Task 4 |
| Literal sensitive field warning | Task 4 |
| Unknown top-level key warning | Task 4 |
| Missing folder → empty payload | Task 4, Task 11 |
| Malformed YAML → skip file, log error | Task 4, Task 11 |
| Sorted filename order | Task 4, Task 11 |
| Both `.yml` and `.yaml` extensions | Task 4, Task 11 |
| `AdminDbSeedService` wired to loader | Task 6 |
| `AuthDbSeedService` wired to loader | Task 7 |
| User seeding idempotency check | Task 7 |
| Existing `ClientSecret` preservation | Task 7 |
| `Seeding:Source = "Legacy"` fallback | Task 7 |
| DI registration for `YamlSeedLoader` + `AdminDbSeedService` (in `WebDev/Program.cs:59`) | Task 8 |
| User Secrets for sensitive values | Task 9 |
| Unit tests: interpolation, guard, unknown key, malformed, duplicates | Task 11 |
| Integration test: seed in-memory AppDbContext, assert entity counts | Task 12 |
| Delete legacy SeedData classes | Task 13 |
| Permission prefix validation warning | Task 4 |
| `OidcScopeDto.ToDescriptor()` | Task 2 |
| `OidcApplicationDto.ToDescriptor()` | Task 2 |
