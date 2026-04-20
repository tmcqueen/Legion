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
        var descriptor = new OpenIddictScopeDescriptor
        {
            Name = "brigade-api",
            Resources = { "brigade-webhost" }
        };

        var existing = await scopeManager.FindByNameAsync("brigade-api", ct);
        if (existing is null)
            await scopeManager.CreateAsync(descriptor, ct);
        else
        {
            await scopeManager.PopulateAsync(existing, descriptor, ct);
            await scopeManager.UpdateAsync(existing, ct);
        }
    }

    private async Task SeedBffApplicationAsync(
        IOpenIddictApplicationManager appManager,
        string authority,
        CancellationToken ct)
    {
        const string clientId = "brigade-bff";

        var secret = configuration["OpenIddict:BffClientSecret"]
            ?? throw new InvalidOperationException("OpenIddict:BffClientSecret is required.");

        var descriptor = new OpenIddictApplicationDescriptor
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
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                "scp:openid",
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Prefixes.Scope + "brigade-api",
            }
        };

        var existing = await appManager.FindByClientIdAsync(clientId, ct);
        if (existing is null)
            await appManager.CreateAsync(descriptor, ct);
        else
        {
            var stored = new OpenIddictApplicationDescriptor();
            await appManager.PopulateAsync(stored, existing, ct);
            descriptor.ClientSecret = stored.ClientSecret;
            await appManager.PopulateAsync(existing, descriptor, ct);
            await appManager.UpdateAsync(existing, ct);
        }
    }

    private async Task SeedApiTestApplicationAsync(
        IOpenIddictApplicationManager appManager,
        CancellationToken ct)
    {
        const string clientId = "brigade-api-test";

        var secret = configuration["OpenIddict:ApiTestClientSecret"]
            ?? throw new InvalidOperationException("OpenIddict:ApiTestClientSecret is required.");

        var descriptor = new OpenIddictApplicationDescriptor
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
        };

        var existing = await appManager.FindByClientIdAsync(clientId, ct);
        if (existing is null)
            await appManager.CreateAsync(descriptor, ct);
        else
        {
            var stored = new OpenIddictApplicationDescriptor();
            await appManager.PopulateAsync(stored, existing, ct);
            descriptor.ClientSecret = stored.ClientSecret;
            await appManager.PopulateAsync(existing, descriptor, ct);
            await appManager.UpdateAsync(existing, ct);
        }
    }
}