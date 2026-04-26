using Brigade.Admin.Data.Auth;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Brigade.Admin.Data;

public static partial class Extensions
{
    public static IServiceCollection AddInMemoryAuthDbContext(this IServiceCollection services) =>
        services.AddDbContext<AuthDbContext>(o => o.UseInMemoryDatabase("AuthDb"));

    public static async Task DoAuthDbMigration(this WebApplication app)
    {
        using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
        await db.Database.MigrateAsync();
    }
}
