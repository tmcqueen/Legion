using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Brigade.Admin.Data.Auth;

namespace Brigade.Admin.Data;

public static class PostgreSqlExtensions
{
    public static IServiceCollection AddPostgreSqlAppDbContext(this IServiceCollection services, string connectionString)
    {
        return services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.MigrationsAssembly("Brigade.Admin.Data.PostgreSQL")));
    }
    
    public static IServiceCollection AddPostgreSqlAuthDbContext(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<AuthDbContext>(o =>
            o.UseNpgsql(connectionString, b => b.MigrationsAssembly("Brigade.Admin.Data.PostgreSQL")));

}
