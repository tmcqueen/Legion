using Legion.Admin.Data.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Legion.Admin.Data;

public static partial class Extensions
{
    public static IServiceCollection AddAuthenticationStateServices(this IServiceCollection services)
    {
                                        
        services.AddCascadingAuthenticationState();
        services.AddScoped<AuthenticationStateProvider, CookieRevalidatingAuthenticationStateProvider>();

        services.AddHostedService<AuthDbSeedService>();

        return services;
    }
}