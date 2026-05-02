using Legion.Admin.Data.PostgreSQL.Stores;
using Legion.Admin.Data.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Legion.Admin.Data.Auth;

namespace Legion.Admin.Data;

public static class PostgreSqlExtensions
{
    public static IServiceCollection AddPostgreSqlAppDbContext(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<ISecretsStore, PostgreSqlSecretsStore>();
        return services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.MigrationsAssembly("Legion.Admin.Data.PostgreSQL")));
    }
    
    public static IServiceCollection AddPostgreSqlAuthDbContext(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<AuthDbContext>(o =>
            o.UseNpgsql(connectionString, b => b.MigrationsAssembly("Legion.Admin.Data.PostgreSQL")));

}
