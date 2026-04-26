using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Validation.AspNetCore;

namespace Brigade.Admin.Data;

public static partial class Extensions
{
    public static IServiceCollection AddAuthorizationServices(this IServiceCollection services)
    {
        
        services.AddAuthorization(options =>
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
        return services;
    }
}