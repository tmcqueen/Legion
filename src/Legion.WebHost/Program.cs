using Legion.WebHost.Components;
using Legion.Admin.Data;
using Legion.WebHost.Data;
using Legion.WebHost.Models;
using Legion.WebHost.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using OpenIddict.Validation.AspNetCore;
using Microsoft.AspNetCore;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();


// AuthDbContext — UseOpenIddict() is in AuthDbContext.OnModelCreating
builder.AddNpgsqlDbContext<AuthDbContext>("authDb");

// Raw Npgsql data source for legion DB (Marten / future use)
builder.AddNpgsqlDataSource("legionDb");

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

    if (builder.Environment.IsDevelopment())
    {
        options.Password.RequiredLength = 4;
        options.Password.RequireDigit = false;
        options.Password.RequireLowercase = false;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
    }
    else
    {
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = true;
    }
});

// OpenIddict — this host IS the authorization server
var authority = builder.Configuration["OpenIddict:Authority"]
    ?? throw new InvalidOperationException("OpenIddict:Authority is required.");

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
               .SetTokenEndpointUris("/connect/token");

        options.AddDevelopmentEncryptionCertificate()
               .AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
               .EnableAuthorizationEndpointPassthrough()
               .EnableTokenEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

// BFF — cookie session for Blazor + OIDC client pointing at this same host

builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = IdentityConstants.ApplicationScheme;
    })
    
    .AddCookie(IdentityConstants.ApplicationScheme, options =>
    {
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
    })

    .AddOpenIdConnect(options =>
    {
        options.Authority = authority;
        options.ClientId = "legion-bff";
        options.ClientSecret = builder.Configuration["OpenIddict:BffClientSecret"]
            ?? throw new InvalidOperationException("OpenIddict:BffClientSecret is required.");
        options.ResponseType = "code";
        options.SaveTokens = true;
        options.Scope.Add("openid");
        options.Scope.Add("profile");
        options.Scope.Add("legion-api");
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

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("BFFPolicy", policy =>
    {
        policy.AddAuthenticationSchemes(
            IdentityConstants.ApplicationScheme,
            OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme)
        .RequireAuthenticatedUser();
    });

    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AuthenticationStateProvider, CookieRevalidatingAuthenticationStateProvider>();

builder.Services.AddHostedService<OpenIddictSeedService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (allowedOrigins is null || allowedOrigins.Length == 0)
        {
            allowedOrigins = ["https://localhost:7000"];
        }
        Console.WriteLine("Allowed CORS origins: {0}", string.Join(", ", allowedOrigins));
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddRadzenComponents();

builder.Services.AddInMemoryAgentDbContext();
builder.Services.AddAgentStores();

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

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor   // If behind a reverse proxy, this will help 
                     | ForwardedHeaders.XForwardedProto // with correct scheme and client IPs in logs, etc.
});

app.UseHttpsRedirection();
app.UseRouting();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets().AllowAnonymous();
app.MapControllers();
app.MapDefaultControllerRoute();
app.MapRazorComponents<App>()
   .AddInteractiveServerRenderMode();


await using (var scope = app.Services.CreateAsyncScope())
{
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
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
                Email = "admin@legion.local",
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
}

app.Run();