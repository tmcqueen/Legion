using Brigade.Admin.Data.Auth;
using Brigade.Admin.Data.Models.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Brigade.Admin.Data;

public static partial class Extensions
{
    public static IServiceCollection AddIdentityCoreServices(this IServiceCollection services)
    {
        // Identity — AddSignInManager() is required for Login.razor
        services.AddIdentityCore<ApplicationUser>()
            .AddEntityFrameworkStores<AuthDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders();
        return services;
    }
}