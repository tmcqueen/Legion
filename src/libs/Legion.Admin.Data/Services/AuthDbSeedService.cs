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
