# OpenIddict BFF + Client Credentials — Design Spec
**Date:** 2026-04-20  
**Project:** Legion.WebHost  
**Status:** Approved

---

## Overview

`Legion.WebHost` (Blazor SSR) acts as three things in a single process:

1. **OpenIddict Authorization Server** — issues tokens for all grant types
2. **BFF (Backend-for-Frontend)** — Blazor authenticates users via Authorization Code + PKCE against its own OpenIddict server; tokens are stored server-side in an encrypted cookie
3. **Resource Server** — API controllers protected by OpenIddict token validation

---

## Architecture

```
Browser ──cookie──► Blazor BFF (AddOpenIdConnect → self)
                         │ bearer token (server-side HttpClient)
                         ▼
                    API Controllers (OpenIddict validation)
                         ▲
External Service ──bearer token (client_credentials)──┘

Both token paths issue from:
  OpenIddict Server (Authorization Code + Client Credentials)
        │
        └── AuthDbContext (Identity + OpenIddict tables, PostgreSQL via Aspire)
```

### Auth paths summary

| Caller | Grant | Result |
|---|---|---|
| Browser user | Authorization Code + PKCE | Cookie (Blazor) + access token stored server-side in cookie |
| Blazor server code → API | Bearer token read from session via `GetTokenAsync` | API sees the user's identity |
| External service | Client Credentials (clientId + secret) | Access token for direct API calls |

---

## Section 1: Fixes to Existing Code

### `Program.cs`

1. **Add `AddSignInManager()` to Identity builder** — `AddIdentityCore` does not include `SignInManager` by default; the Login page requires it:
   ```csharp
   builder.Services.AddIdentityCore<ApplicationUser>()
       .AddEntityFrameworkStores<AuthDbContext>()
       .AddSignInManager()
       .AddDefaultTokenProviders();
   ```

2. **Fix DbContext registration** — use `"authDb"` connection name (not `legionConnectionString`). `UseOpenIddict()` is a `ModelBuilder` extension called in `OnModelCreating`, not a `DbContextOptionsBuilder` extension — do not pass it here:
   ```csharp
   builder.AddNpgsqlDbContext<AuthDbContext>("authDb");
   ```

3. **Add cascading auth state:**
   ```csharp
   builder.Services.AddCascadingAuthenticationState();
   ```

4. **Add cookie + OIDC auth:**
   ```csharp
   builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
       .AddCookie()
       .AddOpenIdConnect(options =>
       {
           options.Authority = builder.Configuration["OpenIddict:Authority"];
           options.ClientId = "legion-bff";
           options.ClientSecret = builder.Configuration["OpenIddict:BffClientSecret"];
           options.ResponseType = OpenIdConnectResponseType.Code;
           options.SaveTokens = true;
           options.Scope.Add("openid");
           options.Scope.Add("profile");
           options.Scope.Add("legion-api");
       });
   ```

5. **Extend OpenIddict server config** — add Authorization Code + PKCE alongside existing Client Credentials:
   ```csharp
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
   ```

### `AuthDbContext.cs`

Add `OnModelCreating` to wire up the OpenIddict EF Core model:
```csharp
protected override void OnModelCreating(ModelBuilder builder)
{
    base.OnModelCreating(builder);
    builder.UseOpenIddict();
}
```

### `Components/Routes.razor`

Replace `<RouteView>` with `<AuthorizeRouteView>` and wrap in `<CascadingAuthenticationState>`:
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

---

## Section 2: New Files

### `Controllers/AuthorizationController.cs`

Handles OpenIddict endpoint passthroughs:
- `GET/POST /connect/authorize` — validates the OIDC request; if user is authenticated issues the authorization code; if not, redirects to the login page with `returnUrl`
- `POST /connect/token` — handles both authorization code exchange and client credentials grant
- `GET/POST /connect/logout` — signs the user out of the cookie and ends the OpenIddict session

### `Controllers/HelloWorldController.cs`

Test API endpoint protected by bearer token validation:
```csharp
[ApiController, Route("api")]
public class HelloWorldController : ControllerBase
{
    [HttpGet("hello"), Authorize]
    public IActionResult Hello() =>
        Ok(new { message = $"Hello, {User.Identity!.Name}!" });
}
```
Protected by the OpenIddict validation middleware (`UseLocalServer` + `UseAspNetCore`). Works for both BFF users (token from session) and external service tokens (client credentials).

### `Components/Pages/Account/Login.razor`

Simple username/password login form:
- Calls `SignInManager<ApplicationUser>.PasswordSignInAsync`
- On success: redirects back to the `returnUrl` (which is the OpenIddict `/connect/authorize` endpoint completing the code flow)
- On failure: shows a validation message
- No registration page — user accounts are seeded or created out-of-band

### `Components/Pages/Account/Logout.razor`

- Signs the user out of the ASP.NET Core cookie
- Redirects to `/connect/logout` to end the OpenIddict session
- Returns to the home page after logout

### `Services/OpenIddictSeedService.cs`

`IHostedService` that runs once on startup and upserts:

**Applications:**

| ClientId | Type | Flow | ConsentType | Notes |
|---|---|---|---|---|
| `legion-bff` | Confidential | AuthorizationCode + PKCE | Implicit | Blazor BFF; redirect URI `https://localhost:7000/signin-oidc`; post-logout `https://localhost:7000/signout-callback-oidc` |
| `legion-api-test` | Confidential | ClientCredentials | — | Test client for hello-world API |

**Scopes:**

| Name | Resources |
|---|---|
| `legion-api` | `legion-webhost` |

Client secrets are read from `IConfiguration` (`OpenIddict:BffClientSecret`, `OpenIddict:ApiTestClientSecret`) — stored in `dotnet user-secrets` for development, environment variables for production. Never in `appsettings.json`.

### `Services/CookieRevalidatingAuthenticationStateProvider.cs`

Extends `RevalidatingServerAuthenticationStateProvider`. For Interactive Server mode, the initial auth state is captured at SignalR connection time. This provider periodically re-checks (every 30 minutes) that the user's claims principal is still valid by verifying the user still exists in Identity (`UserManager.FindByIdAsync`). If the user is not found or the security stamp has changed, the auth state is invalidated and the circuit prompts re-login.

---

## Section 3: Database & Migrations

- **`AuthDbContext.OnModelCreating`** calls `builder.UseOpenIddict()` — this is a model-builder hook, not a schema change, so the existing migration snapshot likely already matches
- **Fix `appsettings.Migrations.json` connection string keys** — current keys are `"legion"` and `"auth"`, but Aspire injects them as `"legionDb"` and `"authDb"`. The keys must match or `dotnet ef migrations add` will fail at design time. Update the file:
  ```json
  {
    "ConnectionStrings": {
      "legionDb": "Host=localhost:5432;Database=legion;Username=postgres;Password=postgres",
      "authDb": "Host=localhost:5432;Database=legion_auth;Username=postgres;Password=postgres"
    }
  }
  ```
- After fixing the DbContext registration, run `dotnet ef migrations add` and verify the resulting migration is a no-op; if it produces changes, include them as a named migration

---

## Section 4: Configuration

### `appsettings.json` / `appsettings.Development.json` (non-secret values only)

```json
{
  "OpenIddict": {
    "Authority": "https://localhost:7000"
  }
}
```

> **Note — Aspire dynamic ports:** Aspire assigns ports dynamically at runtime. Pin the WebHost HTTPS port to `7000` in `Properties/launchSettings.json` under the Aspire/Docker profile so that the Authority URL, the seed service redirect URIs, and `AddOpenIdConnect` all agree on the same host. The seed service should read the authority from `IConfiguration["OpenIddict:Authority"]` and derive redirect URIs from it rather than hard-coding them.

### `dotnet user-secrets` (development)

```
OpenIddict:BffClientSecret = <generated>
OpenIddict:ApiTestClientSecret = <generated>
```

### Aspire `AppHost.cs` (no changes needed)

The `"authDb"` connection name is already wired up to `Legion.WebHost`.

---

## Section 5: Middleware Pipeline Order

The correct order in `Program.cs` (app configuration):

```
UseForwardedHeaders
UseRouting
UseCors
UseAuthentication       ← must come before UseAuthorization
UseAuthorization
UseStatusCodePages
UseHttpsRedirection
UseAntiforgery
MapStaticAssets
MapControllers
MapRazorComponents
```

---

## Out of Scope

- User registration UI (accounts seeded/created out-of-band)
- Role-based authorization on API endpoints
- Token refresh `DelegatingHandler` (add when a protected page is built that actually needs to call the API)
- Production certificate management (dev certs used for now)
- Multi-node / distributed session (single-node Aspire deployment assumed)
- Cookie ticket store — `SaveTokens = true` with Identity profile claims can push the auth cookie past 4 KB; if this becomes an issue, implement `ITicketStore` to store the session server-side
