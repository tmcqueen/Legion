using Brigade.Admin.Data.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brigade.Admin.Data;

public static class SqliteExtensions
{
    public static IServiceCollection AddSqliteAppDbContext(this IServiceCollection services, string connectionString)
    {
        return services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString, o => o.MigrationsAssembly("Brigade.Admin.Data.Sqlite")));
    }    
    
    public static IServiceCollection AddSqliteAuthDbContext(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<AuthDbContext>(o =>
            o.UseSqlite(connectionString, b => b.MigrationsAssembly("Brigade.Admin.Data.Sqlite")));

}
