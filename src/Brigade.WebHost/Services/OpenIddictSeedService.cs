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
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                "openid",
                "profile",
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