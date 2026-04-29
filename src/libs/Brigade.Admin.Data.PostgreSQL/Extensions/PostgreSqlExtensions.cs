using Brigade.Admin.Data.PostgreSQL.Stores;
using Brigade.Admin.Data.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brigade.Admin.Data;

public static class PostgreSqlExtensions
{
    public static IServiceCollection AddPostgreSqlAppDbContext(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<ISecretsStore, PostgreSqlSecretsStore>();
        return services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.MigrationsAssembly("Brigade.Admin.Data.PostgreSQL")));
    }
}
