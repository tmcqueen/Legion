using Brigade.WebHost.Components;
// using Brigade.Agents.Providers;
using Marten;
// using JasperFx;
using Brigade.WebHost.Data;
using Microsoft.EntityFrameworkCore;
using Brigade.WebHost.Models;

using OpenIddict.Abstractions;
using OpenIddict.Core;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.Server;
using OpenIddict.Validation.AspNetCore;
using OpenIddict.Server.AspNetCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

var brigadeConnectionString = builder.Configuration.GetConnectionString("brigadeDb");
var authConnectionString = builder.Configuration.GetConnectionString("authDb");


ArgumentException.ThrowIfNullOrEmpty(brigadeConnectionString, nameof(brigadeConnectionString));
ArgumentException.ThrowIfNullOrEmpty(authConnectionString, nameof(authConnectionString));
builder.Services.AddNpgsqlDataSource(brigadeConnectionString);

Console.WriteLine($"Brigade DB Connection String: {brigadeConnectionString}");
Console.WriteLine($"Auth DB Connection String: {authConnectionString}");

// builder.Services.AddMarten(options =>
// {
//     options.AutoCreateSchemaObjects = AutoCreate.All;
//     options.RegisterDocumentType<AgentOptions>();
// }).UseLightweightSessions().UseNpgsqlDataSource();

builder.Services.AddNpgsql<AuthDbContext>(brigadeConnectionString);

// builder.Services.AddDbContext<AuthDbContext>(options =>
// {
//     options.UseNpgsql(authConnectionString);
//     options.UseOpenIddict();
// });

builder.Services.AddIdentityCore<ApplicationUser>()
    .AddEntityFrameworkStores<AuthDbContext>()
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


builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
            .UseDbContext<AuthDbContext>();
            // .ReplaceDefaultEntities<OpenIddictApplication, OpenIddictAuthorization, OpenIddictScope, OpenIddictToken, Guid>();
    })
    .AddServer(options =>
    {
        options.AllowClientCredentialsFlow();
        options.SetTokenEndpointUris("/auth/token");
        options.AllowClientCredentialsFlow();

        options.AddDevelopmentEncryptionCertificate();
        options.AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
            .EnableTokenEndpointPassthrough();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });
builder.Services.AddControllers();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();



var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
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
    var logger = app.Services.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Application started.");

    using var scope = app.Services.CreateScope();
    var authDbContext = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    logger.LogInformation("Applying database migrations...");
    authDbContext.Database.Migrate();
    logger.LogInformation("Database migrations applied successfully.");
});

// app.Lifetime.ApplicationStarted.Register(async () =>
// {
//     var logger = app.Services.GetRequiredService<ILogger<Program>>();
//     logger.LogInformation("Application started.");
//     var agentStore = app.Services.GetRequiredService<IDocumentStore>();
//     var agentOptions = new AgentOptions
//     {
//         Provider = ProvidersEnum.Anthropic,
//         ApiKey = "test",
//         Model = "claude-opus-4.7",
//         Instructions = "You are a helpful assistant.",
//         Tools = new List<string> { "search", "calculator" },
//         MaxTokens = 1000,
//     };
//     using var session = agentStore.LightweightSession();
//     session.Store(agentOptions);
//     await session.SaveChangesAsync();
//     logger.LogInformation("Seeded initial agent options.");    
// });


app.Run();
