# OpenIddict BFF + Client Credentials Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the OpenIddict authorization server in Brigade.WebHost, enabling cookie-based BFF authentication for Blazor users (Authorization Code + PKCE) and bearer token access for external services (Client Credentials), protected by a hello-world API controller.

**Architecture:** Brigade.WebHost is simultaneously the OpenIddict server, the Blazor BFF, and the resource server. Users authenticate via Authorization Code flow against the same host (self-referencing OIDC); the session cookie holds the tokens server-side. External services POST clientId+secret to `/connect/token` for a bearer token. Both token types are validated by OpenIddict's local validation middleware.

**Tech Stack:** ASP.NET Core 10 / Blazor SSR, OpenIddict 5.x (EF Core), ASP.NET Core Identity, Npgsql + EF Core, .NET Aspire, xUnit

---

## File Map

**Modified:**
- `src/Brigade.WebHost/appsettings.Development.json` — fix connection string key names
- `src/Brigade.WebHost/appsettings.Migrations.json` — fix connection string key names
- `src/Brigade.WebHost/Brigade.WebHost.csproj` — add `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`
- `src/Brigade.WebHost/Data/AuthDbContext.cs` — add `OnModelCreating` / `UseOpenIddict()`
- `src/Brigade.WebHost/Program.cs` — full rewrite: Identity, DbContext, OpenIddict, Cookie+OIDC, middleware pipeline
- `src/Brigade.WebHost/Components/Routes.razor` — `AuthorizeRouteView` + `CascadingAuthenticationState`

**Created:**
- `src/Brigade.WebHost/Controllers/HelloWorldController.cs` — protected `GET /api/hello`
- `src/Brigade.WebHost/Controllers/AuthorizationController.cs` — `/connect/authorize`, `/connect/token`, `/connect/logout`
- `src/Brigade.WebHost/Services/OpenIddictSeedService.cs` — startup seeder for apps + scope
- `src/Brigade.WebHost/Services/CookieRevalidatingAuthenticationStateProvider.cs` — Blazor circuit auth revalidation
- `src/Brigade.WebHost/Components/Pages/Account/Login.razor` — SSR login form
- `src/Brigade.WebHost/Components/Pages/Account/Logout.razor` — SSR sign-out page
- `tests/Brigade.WebHost.Tests/Brigade.WebHost.Tests.csproj` — xUnit test project
- `tests/Brigade.WebHost.Tests/Controllers/HelloWorldControllerTests.cs` — unit tests

---

## Task 1: Fix connection string key names

Aspire injects connection strings as `ConnectionStrings:brigadeDb` and `ConnectionStrings:authDb` (matching the names in `AppHost.cs`). Both `appsettings.Development.json` and `appsettings.Migrations.json` currently use `"brigade"` / `"auth"` which causes `GetConnectionString("brigadeDb")` to return null.

**Files:**
- Modify: `src/Brigade.WebHost/appsettings.Development.json`
- Modify: `src/Brigade.WebHost/appsettings.Migrations.json`

- [ ] **Step 1: Update appsettings.Development.json**

Replace the entire file:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "brigadeDb": "Host=localhost:5432;Database=brigade;Username=postgres;Password=postgres",
    "authDb": "Host=localhost:5432;Database=brigade_auth;Username=postgres;Password=postgres"
  }
}
```

- [ ] **Step 2: Update appsettings.Migrations.json**

Replace the entire file:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "brigadeDb": "Host=localhost:5432;Database=brigade;Username=postgres;Password=postgres",
    "authDb": "Host=localhost:5432;Database=brigade_auth;Username=postgres;Password=postgres"
  }
}
```

- [ ] **Step 3: Commit**

```bash
git add src/Brigade.WebHost/appsettings.Development.json \
        src/Brigade.WebHost/appsettings.Migrations.json
git commit -m "fix: align connection string keys with Aspire names (brigadeDb, authDb)"
```

---

## Task 2: Add Aspire EF Core package

`builder.AddNpgsqlDbContext<T>(connectionName)` is in `Aspire.Npgsql.EntityFrameworkCore.PostgreSQL`, which is distinct from `Aspire.Npgsql` (raw data source). The csproj currently only references `Aspire.Npgsql`.

**Files:**
- Modify: `src/Brigade.WebHost/Brigade.WebHost.csproj`

- [ ] **Step 1: Add the package**

```bash
cd src/Brigade.WebHost
dotnet add package Aspire.Npgsql.EntityFrameworkCore.PostgreSQL
```

- [ ] **Step 2: Verify the entry in the csproj**

Open `src/Brigade.WebHost/Brigade.WebHost.csproj` and confirm:
```xml
<PackageReference Include="Aspire.Npgsql.EntityFrameworkCore.PostgreSQL" />
```

- [ ] **Step 3: Commit**

```bash
git add src/Brigade.WebHost/Brigade.WebHost.csproj
git commit -m "chore: add Aspire.Npgsql.EntityFrameworkCore.PostgreSQL package"
```

---

## Task 3: Fix AuthDbContext — add OnModelCreating

OpenIddict's EF Core integration requires `modelBuilder.UseOpenIddict()` to register its entity type configurations. Without it, the OpenIddict managers can't find their tables.

**Files:**
- Modify: `src/Brigade.WebHost/Data/AuthDbContext.cs`

- [ ] **Step 1: Add OnModelCreating**

Replace the entire file:
```csharp
using Brigade.WebHost.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Brigade.WebHost.Data;

public class AuthDbContext(DbContextOptions<AuthDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.UseOpenIddict();
    }
}
```

- [ ] **Step 2: Check if a new migration is needed**

```bash
cd src/Brigade.WebHost
dotnet ef migrations add verify-openiddict-model --context AuthDbContext \
    -- --environment Migrations
```

Open the generated migration file. If `Up()` is empty (no table creates or alters), the existing schema already matches — remove it:

```bash
dotnet ef migrations remove --context AuthDbContext -- --environment Migrations
```

If `Up()` has real changes, rename and keep the migration:

```bash
# The migration was auto-named; rename the file to something meaningful
# e.g., rename 20260420XXXXXX_verify-openiddict-model.cs
#        → 20260420XXXXXX_add-openiddict-model-config.cs
```

- [ ] **Step 3: Commit**

```bash
git add src/Brigade.WebHost/Data/AuthDbContext.cs \
        src/Brigade.WebHost/Data/Migrations/
git commit -m "fix: add OnModelCreating with UseOpenIddict to AuthDbContext"
```

---

## Task 4: Create test project

**Files:**
- Create: `tests/Brigade.WebHost.Tests/Brigade.WebHost.Tests.csproj`
- Create: `tests/Brigade.WebHost.Tests/Controllers/HelloWorldControllerTests.cs`

- [ ] **Step 1: Create the xUnit project**

```bash
mkdir -p tests/Brigade.WebHost.Tests
cd tests/Brigade.WebHost.Tests
dotnet new xunit --name Brigade.WebHost.Tests --output .
dotnet add reference ../../src/Brigade.WebHost/Brigade.WebHost.csproj
dotnet add package Microsoft.AspNetCore.Mvc.Testing
```

- [ ] **Step 2: Write the failing test**

Create `tests/Brigade.WebHost.Tests/Controllers/HelloWorldControllerTests.cs`:
```csharp
using System.Security.Claims;
using Brigade.WebHost.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Brigade.WebHost.Tests.Controllers;

public class HelloWorldControllerTests
{
    [Fact]
    public void Hello_WithAuthenticatedUser_ReturnsOkWithName()
    {
        var controller = new HelloWorldController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    [new Claim(ClaimTypes.Name, "testuser")], "TestScheme"))
            }
        };

        var result = controller.Hello() as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("testuser", json);
    }

    [Fact]
    public void Hello_WithUnauthenticatedUser_UserNameIsNull()
    {
        var controller = new HelloWorldController();
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity()) // no auth type = unauthenticated
            }
        };

        var result = controller.Hello() as OkObjectResult;

        Assert.NotNull(result);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.Contains("Hello, !", json); // Name is null — real auth is enforced by [Authorize]
    }
}
```

- [ ] **Step 3: Run to confirm it fails (HelloWorldController doesn't exist yet)**

```bash
cd tests/Brigade.WebHost.Tests
dotnet test --filter "HelloWorldControllerTests" 2>&1 | head -20
```

Expected: compile error — `Brigade.WebHost.Controllers.HelloWorldController` not found.

- [ ] **Step 4: Commit**

```bash
git add tests/Brigade.WebHost.Tests/
git commit -m "test: add test project with HelloWorldController failing tests"
```

---

## Task 5: Create HelloWorldController

**Files:**
- Create: `src/Brigade.WebHost/Controllers/HelloWorldController.cs`

- [ ] **Step 1: Create the controller**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Brigade.WebHost.Controllers;

[ApiController, Route("api")]
public class HelloWorldController : ControllerBase
{
    [HttpGet("hello"), Authorize]
    public IActionResult Hello() =>
        Ok(new { message = $"Hello, {User.Identity!.Name}!" });
}
```

- [ ] **Step 2: Run tests**

```bash
cd tests/Brigade.WebHost.Tests
dotnet test --filter "HelloWorldControllerTests" -v
```

Expected: both tests PASS.

- [ ] **Step 3: Commit**

```bash
git add src/Brigade.WebHost/Controllers/HelloWorldController.cs
git commit -m "feat: add HelloWorldController with Authorize-protected GET /api/hello"
```

---

## Task 6: Create AuthorizationController

Handles the three OpenIddict endpoint passthroughs. OpenIddict validates the request; the controller finalises it by inspecting the authenticated user and calling `SignIn`.

**Files:**
- Create: `src/Brigade.WebHost/Controllers/AuthorizationController.cs`

- [ ] **Step 1: Create the file**

```csharp
using System.Security.Claims;
using Brigade.WebHost.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;

namespace Brigade.WebHost.Controllers;

[ApiController]
public class AuthorizationController(
    UserManager<ApplicationUser> userManager,
    IOpenIddictScopeManager scopeManager) : Controller
{
    // Called by the browser to start the Authorization Code flow.
    // If the user has a valid cookie, issue the authorization code.
    // If not, redirect to the login page preserving all OIDC parameters.
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict server request unavailable.");

        var cookieResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        if (!cookieResult.Succeeded)
        {
            // Preserve the full /connect/authorize URL (with all OIDC params) as returnUrl
            var returnUrl = Request.PathBase + Request.Path
                + QueryString.Create(Request.HasFormContentType
                    ? [.. Request.Form]
                    : [.. Request.Query]);

            return Challenge(
                authenticationSchemes: CookieAuthenticationDefaults.AuthenticationScheme,
                properties: new AuthenticationProperties { RedirectUri = returnUrl });
        }

        var user = await userManager.GetUserAsync(cookieResult.Principal)
            ?? throw new InvalidOperationException("Authenticated user not found in database.");

        var identity = new ClaimsIdentity(
            authenticationType: "ExternalLogin",
            nameType: OpenIddictConstants.Claims.Name,
            roleType: OpenIddictConstants.Claims.Role);

        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject,
            await userManager.GetUserIdAsync(user)));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Name,
            await userManager.GetUserNameAsync(user) ?? "unknown"));

        identity.SetScopes(request.GetScopes());
        identity.SetResources(await scopeManager
            .ListResourcesAsync(identity.GetScopes())
            .ToListAsync());

        foreach (var claim in identity.Claims)
            claim.SetDestinations(GetDestinations(claim, identity));

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    // Called server-side by AddOpenIdConnect middleware to exchange the code for tokens.
    // Also called by external services for client_credentials.
    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.GetOpenIddictServerRequest()
            ?? throw new InvalidOperationException("OpenIddict server request unavailable.");

        if (request.IsAuthorizationCodeGrantType())
        {
            var principal = (await HttpContext.AuthenticateAsync(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal!;

            var user = await userManager.FindByIdAsync(
                principal.GetClaim(OpenIddictConstants.Claims.Subject)!);

            if (user is null)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] =
                            OpenIddictConstants.Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The token is no longer valid."
                    }));
            }

            var identity = new ClaimsIdentity(
                principal.Claims,
                authenticationType: "ExternalLogin",
                nameType: OpenIddictConstants.Claims.Name,
                roleType: OpenIddictConstants.Claims.Role);

            identity.SetClaim(OpenIddictConstants.Claims.Subject,
                await userManager.GetUserIdAsync(user));
            identity.SetClaim(OpenIddictConstants.Claims.Name,
                await userManager.GetUserNameAsync(user));

            foreach (var claim in identity.Claims)
                claim.SetDestinations(GetDestinations(claim, identity));

            return SignIn(new ClaimsPrincipal(identity),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsClientCredentialsGrantType())
        {
            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            identity.AddClaim(OpenIddictConstants.Claims.Subject, request.ClientId!);
            identity.AddClaim(OpenIddictConstants.Claims.Name, request.ClientId!);

            identity.SetScopes(request.GetScopes());
            identity.SetResources(await scopeManager
                .ListResourcesAsync(identity.GetScopes())
                .ToListAsync());

            foreach (var claim in identity.Claims)
                claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);

            return SignIn(new ClaimsPrincipal(identity),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        throw new InvalidOperationException($"Unsupported grant type: {request.GrantType}");
    }

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties { RedirectUri = "/" });
    }

    private static IEnumerable<string> GetDestinations(Claim claim, ClaimsIdentity identity) =>
        claim.Type switch
        {
            OpenIddictConstants.Claims.Subject =>
                [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.Name when identity.HasScope(OpenIddictConstants.Scopes.Profile) =>
                [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            _ => [OpenIddictConstants.Destinations.AccessToken]
        };
}
```

- [ ] **Step 2: Build**

```bash
cd src/Brigade.WebHost
dotnet build
```

Expected: success. All OpenIddict types resolve from existing packages.

- [ ] **Step 3: Commit**

```bash
git add src/Brigade.WebHost/Controllers/AuthorizationController.cs
git commit -m "feat: add AuthorizationController for OIDC endpoint passthroughs"
```

---

## Task 7: Create OpenIddictSeedService

Registers the two OIDC applications and the `brigade-api` scope on startup, idempotently.

**Files:**
- Create: `src/Brigade.WebHost/Services/OpenIddictSeedService.cs`

- [ ] **Step 1: Create the file**

```csharp
using OpenIddict.Abstractions;

namespace Brigade.WebHost.Services;

public sealed class OpenIddictSeedService(
    IServiceProvider serviceProvider,
    IConfiguration configuration,
    ILogger<OpenIddictSeedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var appManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();

        var authority = configuration["OpenIddict:Authority"]
            ?? throw new InvalidOperationException("OpenIddict:Authority is required in configuration.");

        await SeedScopeAsync(scopeManager, cancellationToken);
        await SeedBffApplicationAsync(appManager, authority, cancellationToken);
        await SeedApiTestApplicationAsync(appManager, cancellationToken);

        logger.LogInformation("OpenIddict seed complete.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedScopeAsync(IOpenIddictScopeManager scopeManager, CancellationToken ct)
    {
        if (await scopeManager.FindByNameAsync("brigade-api", ct) is not null) return;

        await scopeManager.CreateAsync(new OpenIddictScopeDescriptor
        {
            Name = "brigade-api",
            Resources = { "brigade-webhost" }
        }, ct);
    }

    private async Task SeedBffApplicationAsync(
        IOpenIddictApplicationManager appManager,
        string authority,
        CancellationToken ct)
    {
        const string clientId = "brigade-bff";
        if (await appManager.FindByClientIdAsync(clientId, ct) is not null) return;

        var secret = configuration["OpenIddict:BffClientSecret"]
            ?? throw new InvalidOperationException("OpenIddict:BffClientSecret is required.");

        await appManager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = secret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            DisplayName = "Brigade BFF",
            RedirectUris = { new Uri($"{authority}/signin-oidc") },
            PostLogoutRedirectUris = { new Uri($"{authority}/signout-callback-oidc") },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.Logout,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                OpenIddictConstants.Permissions.Scopes.OpenId,
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Prefixes.Scope + "brigade-api",
            }
        }, ct);
    }

    private async Task SeedApiTestApplicationAsync(
        IOpenIddictApplicationManager appManager,
        CancellationToken ct)
    {
        const string clientId = "brigade-api-test";
        if (await appManager.FindByClientIdAsync(clientId, ct) is not null) return;

        var secret = configuration["OpenIddict:ApiTestClientSecret"]
            ?? throw new InvalidOperationException("OpenIddict:ApiTestClientSecret is required.");

        await appManager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = clientId,
            ClientSecret = secret,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            DisplayName = "Brigade API Test Client",
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Prefixes.Scope + "brigade-api",
            }
        }, ct);
    }
}
```

- [ ] **Step 2: Build**

```bash
cd src/Brigade.WebHost
dotnet build
```

Expected: success.

- [ ] **Step 3: Commit**

```bash
git add src/Brigade.WebHost/Services/OpenIddictSeedService.cs
git commit -m "feat: add OpenIddictSeedService to seed BFF and API test applications on startup"
```

---

## Task 8: Create CookieRevalidatingAuthenticationStateProvider

For Blazor Interactive Server mode, the initial `ClaimsPrincipal` is captured at SignalR circuit connection. If the session expires mid-session, the circuit won't notice without this provider.

**Files:**
- Create: `src/Brigade.WebHost/Services/CookieRevalidatingAuthenticationStateProvider.cs`

- [ ] **Step 1: Create the file**

```csharp
using System.Security.Claims;
using Brigade.WebHost.Models;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

namespace Brigade.WebHost.Services;

public sealed class CookieRevalidatingAuthenticationStateProvider(
    ILoggerFactory loggerFactory,
    IServiceScopeFactory scopeFactory)
    : RevalidatingServerAuthenticationStateProvider(loggerFactory)
{
    protected override TimeSpan RevalidationInterval => TimeSpan.FromMinutes(30);

    protected override async Task<bool> ValidateAuthenticationStateAsync(
        AuthenticationState authenticationState,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        // The subject claim comes from the OIDC token, not from Identity directly.
        var userId =
            authenticationState.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? authenticationState.User.FindFirstValue(OpenIddictConstants.Claims.Subject);

        if (userId is null) return false;

        var user = await userManager.FindByIdAsync(userId);
        return user is not null;
    }
}
```

- [ ] **Step 2: Build**

```bash
cd src/Brigade.WebHost
dotnet build
```

Expected: success. `RevalidatingServerAuthenticationStateProvider` is in `Microsoft.AspNetCore.Components.Server` which ships with the ASP.NET Core framework.

- [ ] **Step 3: Commit**

```bash
git add src/Brigade.WebHost/Services/CookieRevalidatingAuthenticationStateProvider.cs
git commit -m "feat: add CookieRevalidatingAuthenticationStateProvider for Blazor circuit auth revalidation"
```

---

## Task 9: Create Login.razor

SSR Blazor page at `/account/login`. The form POSTs back to itself; `[SupplyParameterFromForm]` intercepts it before rendering, calls `SignInManager`, then `NavigationManager.NavigateTo` issues a 302 redirect back to the OpenIddict authorize endpoint.

**Files:**
- Create: `src/Brigade.WebHost/Components/Pages/Account/Login.razor`

- [ ] **Step 1: Create the file**

```razor
@page "/account/login"
@using System.ComponentModel.DataAnnotations
@using Brigade.WebHost.Models
@using Microsoft.AspNetCore.Identity
@inject SignInManager<ApplicationUser> SignInManager
@inject NavigationManager NavigationManager
@inject ILogger<Login> Logger

<PageTitle>Sign in — Brigade</PageTitle>

<h1>Sign in</h1>

@if (ErrorMessage is not null)
{
    <div class="alert alert-danger" role="alert">@ErrorMessage</div>
}

<EditForm Model="Model" method="post" OnValidSubmit="OnValidSubmitAsync" FormName="login">
    <DataAnnotationsValidator />
    <ValidationSummary />
    <div class="mb-3">
        <label for="username" class="form-label">Username</label>
        <InputText @bind-Value="Model.Username" id="username" class="form-control"
                   autocomplete="username" />
        <ValidationMessage For="() => Model.Username" />
    </div>
    <div class="mb-3">
        <label for="password" class="form-label">Password</label>
        <InputText @bind-Value="Model.Password" id="password" type="password"
                   class="form-control" autocomplete="current-password" />
        <ValidationMessage For="() => Model.Password" />
    </div>
    <button type="submit" class="btn btn-primary">Sign in</button>
</EditForm>

@code {
    [SupplyParameterFromQuery]
    public string? ReturnUrl { get; set; }

    [SupplyParameterFromForm]
    public LoginModel Model { get; set; } = new();

    private string? ErrorMessage { get; set; }

    public async Task OnValidSubmitAsync()
    {
        var result = await SignInManager.PasswordSignInAsync(
            Model.Username, Model.Password, isPersistent: false, lockoutOnFailure: false);

        if (result.Succeeded)
        {
            Logger.LogInformation("User {Username} signed in.", Model.Username);
            NavigationManager.NavigateTo(ReturnUrl ?? "/");
        }
        else
        {
            ErrorMessage = "Invalid username or password.";
        }
    }

    public sealed class LoginModel
    {
        [Required]
        public string Username { get; set; } = "";

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = "";
    }
}
```

- [ ] **Step 2: Build**

```bash
cd src/Brigade.WebHost
dotnet build
```

Expected: success.

- [ ] **Step 3: Commit**

```bash
git add src/Brigade.WebHost/Components/Pages/Account/Login.razor
git commit -m "feat: add SSR Login.razor for OIDC authorization code flow"
```

---

## Task 10: Create Logout.razor

Calls `HttpContext.SignOutAsync` on first render (SSR only) then redirects home. `IHttpContextAccessor` is required because Blazor components don't have direct access to `HttpContext`.

**Files:**
- Create: `src/Brigade.WebHost/Components/Pages/Account/Logout.razor`

- [ ] **Step 1: Create the file**

```razor
@page "/account/logout"
@using Microsoft.AspNetCore.Authentication
@using Microsoft.AspNetCore.Authentication.Cookies
@inject IHttpContextAccessor HttpContextAccessor
@inject NavigationManager NavigationManager

@code {
    protected override async Task OnInitializedAsync()
    {
        if (HttpContextAccessor.HttpContext is { } ctx)
        {
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        }
        NavigationManager.NavigateTo("/");
    }
}
```

- [ ] **Step 2: Build**

```bash
cd src/Brigade.WebHost
dotnet build
```

Expected: success.

- [ ] **Step 3: Commit**

```bash
git add src/Brigade.WebHost/Components/Pages/Account/Logout.razor
git commit -m "feat: add Logout.razor to sign out of the cookie session"
```

---

## Task 11: Rewrite Program.cs

All service classes from previous tasks now exist, so Program.cs can reference them cleanly. This replaces the incomplete existing version entirely.

**Files:**
- Modify: `src/Brigade.WebHost/Program.cs`

- [ ] **Step 1: Replace the entire file**

```csharp
using Brigade.WebHost.Components;
using Brigade.WebHost.Data;
using Brigade.WebHost.Models;
using Brigade.WebHost.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Abstractions;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// AuthDbContext — UseOpenIddict() is in AuthDbContext.OnModelCreating
builder.AddNpgsqlDbContext<AuthDbContext>("authDb");

// Raw Npgsql data source for brigade DB (Marten / future use)
builder.AddNpgsqlDataSource("brigadeDb");

// Identity — AddSignInManager() is required for Login.razor
builder.Services.AddIdentityCore<ApplicationUser>()
    .AddEntityFrameworkStores<AuthDbContext>()
    .AddSignInManager()
    .AddDefaultTokenProviders();

builder.Services.Configure<IdentityOptions>(options =>
{
    options.ClaimsIdentity.UserNameClaimType = OpenIddictConstants.Claims.Name;
    options.ClaimsIdentity.UserIdClaimType = OpenIddictConstants.Claims.Subject;
    options.ClaimsIdentity.RoleClaimType = OpenIddictConstants.Claims.Role;
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
});

// OpenIddict — this host IS the authorization server
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore().UseDbContext<AuthDbContext>();
    })
    .AddServer(options =>
    {
        options.AllowAuthorizationCodeFlow().RequireProofKeyForCodeExchange();
        options.AllowClientCredentialsFlow();

        options.SetAuthorizationEndpointUris("/connect/authorize")
               .SetTokenEndpointUris("/connect/token")
               .SetLogoutEndpointUris("/connect/logout");

        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough()
               .EnableLogoutEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// BFF — cookie session for Blazor + OIDC client pointing at this same host
var authority = builder.Configuration["OpenIddict:Authority"]
    ?? throw new InvalidOperationException("OpenIddict:Authority is required.");

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
    })
    .AddOpenIdConnect(options =>
    {
        options.Authority = authority;
        options.ClientId = "brigade-bff";
        options.ClientSecret = builder.Configuration["OpenIddict:BffClientSecret"]
            ?? throw new InvalidOperationException("OpenIddict:BffClientSecret is required.");
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("brigade-api");
        // Allow self-signed dev certs for the backchannel call (same host → same cert)
        if (builder.Environment.IsDevelopment())
        {
            options.RequireHttpsMetadata = false;
            options.BackchannelHttpHandler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
        }
    });

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, CookieRevalidatingAuthenticationStateProvider>();

builder.Services.AddHostedService<OpenIddictSeedService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseForwardedHeaders();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();
app.UseAntiforgery();

app.MapStaticAssets();
app.MapControllers();
app.MapDefaultControllerRoute();
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();

app.Lifetime.ApplicationStarted.Register(() =>
{
    Task.Run(async () =>
    {
        var logger = app.Services.GetRequiredService<ILogger<Program>>();
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        logger.LogInformation("Applying database migrations...");
        await db.Database.MigrateAsync();
        logger.LogInformation("Database migrations applied.");

        if (app.Environment.IsDevelopment())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            const string seedUser = "admin";
            const string seedPass = "Admin1234!";
            if (await userManager.FindByNameAsync(seedUser) is null)
            {
                var user = new ApplicationUser
                {
                    UserName = seedUser,
                    Email = "admin@brigade.local",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(user, seedPass);
                if (result.Succeeded)
                    logger.LogInformation("Seed user '{User}' created.", seedUser);
                else
                    logger.LogWarning("Seed user failed: {Errors}",
                        string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }).GetAwaiter().GetResult();
});

app.Run();
```

- [ ] **Step 2: Build**

```bash
cd src/Brigade.WebHost
dotnet build
```

Expected: clean build — no CS errors. If the `brigade-bff` OIDC client secret is missing, it will throw at startup (not at build time).

- [ ] **Step 3: Commit**

```bash
git add src/Brigade.WebHost/Program.cs
git commit -m "feat: rewrite Program.cs — wire up auth, OpenIddict server, seed service, BFF"
```

---

## Task 12: Fix Routes.razor

Replace `<RouteView>` with `<AuthorizeRouteView>` so `[Authorize]` on Blazor pages triggers the login redirect instead of showing a blank page.

**Files:**
- Modify: `src/Brigade.WebHost/Components/Routes.razor`

- [ ] **Step 1: Replace the file**

```razor
<CascadingAuthenticationState>
    <Router AppAssembly="typeof(Program).Assembly" NotFoundPage="typeof(Pages.NotFound)">
        <Found Context="routeData">
            <AuthorizeRouteView RouteData="routeData" DefaultLayout="typeof(Layout.MainLayout)" />
            <FocusOnNavigate RouteData="routeData" Selector="h1" />
        </Found>
    </Router>
</CascadingAuthenticationState>
```

- [ ] **Step 2: Build**

```bash
cd src/Brigade.WebHost
dotnet build
```

Expected: success.

- [ ] **Step 3: Commit**

```bash
git add src/Brigade.WebHost/Components/Routes.razor
git commit -m "fix: use AuthorizeRouteView in Routes.razor to support Blazor page-level auth"
```

---

## Task 13: Add OpenIddict:Authority to appsettings + set user secrets

The `Authority` value and client secrets must be set before the app can start.

**Files:**
- Modify: `src/Brigade.WebHost/appsettings.Development.json`

- [ ] **Step 1: Add the Authority to appsettings.Development.json**

Merge the `OpenIddict` section into the existing file:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "ConnectionStrings": {
    "brigadeDb": "Host=localhost:5432;Database=brigade;Username=postgres;Password=postgres",
    "authDb": "Host=localhost:5432;Database=brigade_auth;Username=postgres;Password=postgres"
  },
  "OpenIddict": {
    "Authority": "https://localhost:7000"
  }
}
```

Port 7000 is already pinned in `Properties/launchSettings.json` (`https` profile).

- [ ] **Step 2: Set user secrets**

```bash
cd src/Brigade.WebHost
dotnet user-secrets set "OpenIddict:BffClientSecret" "$(openssl rand -base64 32)"
dotnet user-secrets set "OpenIddict:ApiTestClientSecret" "$(openssl rand -base64 32)"
```

Record the generated secrets somewhere safe — you'll need them if you need to reset or inspect the seeded applications.

- [ ] **Step 3: Commit (appsettings only — secrets stay local)**

```bash
git add src/Brigade.WebHost/appsettings.Development.json
git commit -m "config: add OpenIddict:Authority for development"
```

---

## Task 14: End-to-end smoke test

Verify both auth paths work with a running Aspire stack.

**Prerequisites:** PostgreSQL running via Aspire (`dotnet run` in `src/Brigade.AppHost`). User secrets set in Task 13.

- [ ] **Step 1: Start Aspire**

```bash
cd src/Brigade.AppHost
dotnet run
```

Watch the console for:
```
Applying database migrations...
Database migrations applied.
Seed user 'admin' created.
OpenIddict seed complete.
```

If the seed service throws `OpenIddict:BffClientSecret is required`, the user secrets from Task 13 were not set in the correct project directory.

- [ ] **Step 2: Test the BFF login flow in a browser**

1. Open `https://localhost:7000`
2. Navigate to `https://localhost:7000/api/hello` — the browser should redirect to `/account/login`
3. Sign in with `admin` / `Admin1234!`
4. After login, you should be redirected back to `/api/hello` and see:
   ```json
   {"message":"Hello, admin!"}
   ```

- [ ] **Step 3: Test Client Credentials from the command line**

```bash
# Retrieve the ApiTestClientSecret you generated in Task 13
API_SECRET=$(dotnet user-secrets get "OpenIddict:ApiTestClientSecret" \
    --project src/Brigade.WebHost 2>/dev/null || echo "<paste-secret-here>")

# Request a token
curl -sk -X POST https://localhost:7000/connect/token \
  -H "Content-Type: application/x-www-form-urlencoded" \
  -d "grant_type=client_credentials&client_id=brigade-api-test&client_secret=${API_SECRET}&scope=brigade-api"
```

Expected: JSON with `access_token`, `token_type: "Bearer"`, `expires_in`.

- [ ] **Step 4: Call the API with the bearer token**

```bash
TOKEN="<paste access_token from step 3>"
curl -sk https://localhost:7000/api/hello \
  -H "Authorization: Bearer ${TOKEN}"
```

Expected:
```json
{"message":"Hello, brigade-api-test!"}
```

- [ ] **Step 5: Commit any fixes discovered during smoke testing**

---

## Self-review notes

- **`BackchannelHttpHandler`** in Task 11 uses `DangerousAcceptAnyServerCertificateValidator` in development. This is intentional: the `AddOpenIdConnect` middleware makes a server-to-server backchannel call from this host to itself (`https://localhost:7000/connect/token`). The dev cert is self-signed and won't pass standard validation in that backchannel. In production, use a properly signed cert and remove this override.
- **`OpenIddictScopePermission`** for `openid`: `OpenIddictConstants.Permissions.Scopes.OpenId` is added to the BFF application in the seed service. Without it, OpenIddict will reject the `openid` scope in the authorization request.
- **Seed user password** `Admin1234!` is dev-only. It is created inside an `if (app.Environment.IsDevelopment())` guard and never runs in production.
- **Migration key `"authDb"`**: if `dotnet ef migrations add` fails with a null connection string, confirm the `--environment Migrations` flag is being passed and that `appsettings.Migrations.json` was updated in Task 1.
