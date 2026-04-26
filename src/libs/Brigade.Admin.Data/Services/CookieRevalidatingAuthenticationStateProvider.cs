using System.Security.Claims;
using Brigade.Admin.Data.Auth;
using Brigade.Admin.Data.Models.Auth;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace Brigade.Admin.Data.Services;

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

        var userId =
            authenticationState.User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? authenticationState.User.FindFirstValue(OpenIddictConstants.Claims.Subject);

        if (userId is null) return false;

        var user = await userManager.FindByIdAsync(userId);
        return user is not null;
    }
}