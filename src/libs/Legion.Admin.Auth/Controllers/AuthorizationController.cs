using System.Security.Claims;
using Legion.Admin.Data.Models.Auth;

// using Legion.Admin.Data.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using IOpenIddictServerFeature = OpenIddict.Server.AspNetCore.OpenIddictServerAspNetCoreFeature;

namespace Legion.Admin.Auth.Controllers;

[ApiController, AllowAnonymous]
public class AuthorizationController(
    UserManager<ApplicationUser> userManager,
    IOpenIddictScopeManager scopeManager) : Controller
{
    [HttpGet("~/connect/authorize")]
    [HttpPost("~/connect/authorize")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Authorize()
    {
        var request = HttpContext.Features.Get<IOpenIddictServerFeature>()?.Transaction?.Request
            ?? throw new InvalidOperationException("OpenIddict server request unavailable.");

        var cookieResult = await HttpContext.AuthenticateAsync(IdentityConstants.ApplicationScheme);

        if (!cookieResult.Succeeded)
        {
            var returnUrl = Request.PathBase + Request.Path
                + QueryString.Create(Request.HasFormContentType
                    ? [.. Request.Form]
                    : [.. Request.Query]);

            return Challenge(
                authenticationSchemes: IdentityConstants.ApplicationScheme,
                properties: new AuthenticationProperties { RedirectUri = returnUrl });
        }

        var user = await userManager.GetUserAsync(cookieResult.Principal)
            ?? throw new InvalidOperationException("Authenticated user not found in database.");

        var identity = new ClaimsIdentity(
            authenticationType: "ExternalLogin",
            nameType: OpenIddictConstants.Claims.Name,
            roleType: OpenIddictConstants.Claims.Role);

        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject,
            await userManager.GetUserIdAsync(user)));
        identity.AddClaim(new Claim(OpenIddictConstants.Claims.Name,
            await userManager.GetUserNameAsync(user) ?? "unknown"));

        identity.SetScopes(request.GetScopes());
        identity.SetResources(await scopeManager
            .ListResourcesAsync(identity.GetScopes())
            .ToListAsync());

        foreach (var claim in identity.Claims)
            claim.SetDestinations(GetDestinations(claim, identity));

        return SignIn(new ClaimsPrincipal(identity), OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }

    [HttpPost("~/connect/token")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Exchange()
    {
        var request = HttpContext.Features.Get<IOpenIddictServerFeature>()?.Transaction?.Request
            ?? throw new InvalidOperationException("OpenIddict server request unavailable.");

        if (request.IsAuthorizationCodeGrantType())
        {
            var principal = (await HttpContext.AuthenticateAsync(
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme)).Principal!;

            var user = await userManager.FindByIdAsync(
                principal.GetClaim(OpenIddictConstants.Claims.Subject)!);

            if (user is null)
            {
                return Forbid(
                    authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
                    properties: new AuthenticationProperties(new Dictionary<string, string?>
                    {
                        [OpenIddictServerAspNetCoreConstants.Properties.Error] =
                            OpenIddictConstants.Errors.InvalidGrant,
                        [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                            "The token is no longer valid."
                    }));
            }

            var identity = new ClaimsIdentity(
                principal.Claims,
                authenticationType: "ExternalLogin",
                nameType: OpenIddictConstants.Claims.Name,
                roleType: OpenIddictConstants.Claims.Role);

            identity.SetClaim(OpenIddictConstants.Claims.Subject,
                await userManager.GetUserIdAsync(user));
            identity.SetClaim(OpenIddictConstants.Claims.Name,
                await userManager.GetUserNameAsync(user));

            foreach (var claim in identity.Claims)
                claim.SetDestinations(GetDestinations(claim, identity));

            return SignIn(new ClaimsPrincipal(identity),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        if (request.IsClientCredentialsGrantType())
        {
            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
            identity.AddClaim(OpenIddictConstants.Claims.Subject, request.ClientId!);
            identity.AddClaim(OpenIddictConstants.Claims.Name, request.ClientId!);

            identity.SetScopes(request.GetScopes());
            identity.SetResources(await scopeManager
                .ListResourcesAsync(identity.GetScopes())
                .ToListAsync());

            foreach (var claim in identity.Claims)
                claim.SetDestinations(OpenIddictConstants.Destinations.AccessToken);

            return SignIn(new ClaimsPrincipal(identity),
                OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        return Forbid(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties(new Dictionary<string, string?>
            {
                [OpenIddictServerAspNetCoreConstants.Properties.Error] =
                    OpenIddictConstants.Errors.UnsupportedGrantType,
                [OpenIddictServerAspNetCoreConstants.Properties.ErrorDescription] =
                    $"Unsupported grant type: {request.GrantType}"
            }));
    }

    [HttpGet("~/connect/logout")]
    [HttpPost("~/connect/logout")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
        return SignOut(
            authenticationSchemes: OpenIddictServerAspNetCoreDefaults.AuthenticationScheme,
            properties: new AuthenticationProperties { RedirectUri = "/" });
    }

    private static IEnumerable<string> GetDestinations(Claim claim, ClaimsIdentity identity) =>
        claim.Type switch
        {
            OpenIddictConstants.Claims.Subject =>
                [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            OpenIddictConstants.Claims.Name when identity.HasScope(OpenIddictConstants.Scopes.Profile) =>
                [OpenIddictConstants.Destinations.AccessToken, OpenIddictConstants.Destinations.IdentityToken],
            _ => [OpenIddictConstants.Destinations.AccessToken]
        };
}