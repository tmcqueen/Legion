using Brigade.WebHost.Components;
using Brigade.WebHost.Data;
using Brigade.WebHost.Models;
using Brigade.WebHost.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
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
        db.Database.Migrate();
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