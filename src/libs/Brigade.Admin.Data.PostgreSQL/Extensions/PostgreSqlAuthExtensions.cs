using Brigade.Admin.Data.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brigade.Admin.Data;

public static class PostgreSqlAuthExtensions
{
    public static IServiceCollection AddPostgreSqlAuthDbContext(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<AuthDbContext>(o =>
            o.UseNpgsql(connectionString, b => b.MigrationsAssembly("Brigade.Admin.Data.PostgreSQL")));
}
