using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brigade.Admin.Data;

public static class PostgreSqlExtensions
{
    public static IServiceCollection AddPostgreSqlAppDbContext(this IServiceCollection services, string connectionString)
    {
        return services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString, o => o.MigrationsAssembly("Brigade.Admin.Data.PostgreSQL")));
    }
}
