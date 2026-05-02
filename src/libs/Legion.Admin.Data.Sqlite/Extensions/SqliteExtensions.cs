using Legion.Admin.Data.Auth;
using Legion.Admin.Data.Sqlite.Stores;
using Legion.Admin.Data.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Legion.Admin.Data;

public static class SqliteExtensions
{
    public static IServiceCollection AddSqliteAppDbContext(this IServiceCollection services, string connectionString)
    {
        services.AddScoped<ISecretsStore, SqliteSecretsStore>();
        services.AddScoped<IPromptStore, PromptStore>();
        return services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString, o => o.MigrationsAssembly("Legion.Admin.Data.Sqlite")));
    }    
    
    public static IServiceCollection AddSqliteAuthDbContext(this IServiceCollection services, string connectionString) =>
        services.AddDbContext<AuthDbContext>(o =>
            o.UseSqlite(connectionString, b => b.MigrationsAssembly("Legion.Admin.Data.Sqlite")));

}
