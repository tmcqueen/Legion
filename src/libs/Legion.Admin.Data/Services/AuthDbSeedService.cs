
using Legion.Admin.Data.Seeds;
using Legion.Admin.Data.Auth;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;
using Legion.Admin.Data.Models.Auth;

namespace Legion.Admin.Data.Services;

public class AuthDbSeedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly string _env;
    private readonly ILogger<AuthDbSeedService> _logger;
    private readonly string _authority;

    public AuthDbSeedService(ILogger<AuthDbSeedService> logger,
                             IServiceProvider serviceProvider,
                             IWebHostEnvironment env,
                             IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _env = env.EnvironmentName;
        _logger = logger;
        _authority = configuration["OpenIddict:Authority"]           
            ?? throw new InvalidOperationException("OpenIddict:Authority is required in configuration.")   ;
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(); 
        var appManager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var scopeManager = scope.ServiceProvider.GetRequiredService<IOpenIddictScopeManager>();
        // var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        // Seed the database with initial data if necessary
        if (_env == "Development")
        {
            await SeedUsersAsync(SeedData.GetDefaultAppUsers(), userManager);
            await SeedApplicationsAsync(SeedData.GetDefaultApplications(_authority), appManager);

            await SeedScopesAsync(SeedData.GetDefaultAppScopes(), scopeManager);
            // await SeedRolesAsync(SeedData.GetDefaultRoles(), roleManager);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedUsersAsync(List<SeedData.SeedUser> defaultUsers, UserManager<ApplicationUser> userManager)
    {
        foreach (var seedUser in defaultUsers)
        {
            var user = seedUser.ToApplicationUser();

            var result = await userManager.CreateAsync(user, seedUser.Password);
            if (!result.Succeeded)
            {
                _logger.LogError("Failed to create user {UserName}: {Errors}",
                    user.UserName, string.Join(", ", result.Errors.Select(e => e.Description)));
            }
        }
    }

    private async Task SeedApplicationsAsync(List<OpenIddictApplicationDescriptor> defaultApps, IOpenIddictApplicationManager appManager, CancellationToken ct = default)
    {
        foreach (var app in defaultApps)
        {
            if (app is null) continue;
            if (app.ClientId is null) continue;
            var existing = await appManager.FindByClientIdAsync(app.ClientId, ct);
            if (existing is null)
                await appManager.CreateAsync(app, ct);
            else
            {
                var stored = new OpenIddictApplicationDescriptor();
                await appManager.PopulateAsync(stored, existing, ct);
                app.ClientSecret = stored.ClientSecret;
                await appManager.PopulateAsync(existing, app, ct);
                await appManager.UpdateAsync(existing, ct);
            }
        }
    }

    private async Task SeedScopesAsync(List<OpenIddictScopeDescriptor> defaultScopes, IOpenIddictScopeManager scopeManager, CancellationToken ct = default)
    {
        foreach (var descriptor in defaultScopes)
        {
            if (descriptor is null) continue;
            if (descriptor.Name is null) continue;
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

    private async Task SeedRolesAsync(List<string> defaultRoles, RoleManager<IdentityRole> roleManager)
    {
        foreach (var roleName in defaultRoles)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole(roleName));
                if (!result.Succeeded)
                {
                    _logger.LogError("Failed to create role {RoleName}: {Errors}",
                        roleName, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }


}   

