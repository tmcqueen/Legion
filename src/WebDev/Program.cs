using System.Text.Json.Serialization;
using WebDev.Components;
using WebDev.Services;
using Legion.Admin.Auth;
using Microsoft.AspNetCore.HttpOverrides;
using Radzen;
using Legion.Admin.Data;
using Legion.Admin.Data.Services;
using Legion.Admin.Data.Seeds;

var builder = WebApplication.CreateBuilder(args);

// builder.Services.AddInMemoryAuthDbContext();
string authDbConnectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=auth.db";
builder.Services.AddSqliteAuthDbContext(authDbConnectionString);

builder.Services.AddIdentityCoreServices();
builder.Services.ConfigureIdentityOptions(builder.Environment.IsDevelopment());
builder.Services.AddOpenIddictServices();
builder.AddAuthenticationServices()
       .AddAuthorizationServices();

builder.Services.AddAuthenticationStateServices();

builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<DialogService>();
builder.Services.AddScoped<TooltipService>();
builder.Services.AddScoped<ContextMenuService>();

string agentDbConnectionString = builder.Configuration.GetConnectionString("Default") ?? "Data Source=agent.db";
builder.Services.AddSqliteAppDbContext(agentDbConnectionString);
// builder.Services.AddInMemoryAgentDbContext();
builder.Services.AddAgentStores();

builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddScoped<ImportService>();
builder.Services.AddHttpClient("import")
    .ConfigurePrimaryHttpMessageHandler(() =>
        new SocketsHttpHandler { AllowAutoRedirect = false });

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

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

builder.Services.AddSingleton<YamlSeedLoader>();
builder.Services.AddHostedService<AdminDbSeedService>();
builder.Services.AddHostedService<AuthDbSeedService>();

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
app.MapStaticAssets().AllowAnonymous();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapControllers();
app.MapDefaultControllerRoute();
app.MapRazorComponents<App>()
   .AddAdditionalAssemblies(typeof(Legion.Admin.UI.Pages.Login).Assembly)
   .AddInteractiveServerRenderMode();


await app.DoAuthDbMigration();
await app.DoAgentDbMigration();

app.Run();
