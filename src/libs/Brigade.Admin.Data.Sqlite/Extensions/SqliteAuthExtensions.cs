using Brigade.Admin.Data.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brigade.Admin.Data;

public static class SqliteAuthExtensions
{
    public static IServiceCollection AddSqliteAuthDbContext(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<AuthDbContext>(o =>
            o.UseSqlite(connectionString, b => b.MigrationsAssembly("Brigade.Admin.Data.Sqlite")));
}
