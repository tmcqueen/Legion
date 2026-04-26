using Brigade.Admin.Data.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Brigade.Admin.Data.Sqlite.Migrations;

public class AuthDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlite("Data Source=brigade-auth.db",
                o => o.MigrationsAssembly("Brigade.Admin.Data.Sqlite"))
            .Options;
        return new AuthDbContext(options);
    }
}
