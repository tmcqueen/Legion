using Brigade.Admin.Data.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Brigade.Admin.Data.Sqlite;

public class AuthDbDesignTimeFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlite("Data Source=brigade.db",
                o => o.MigrationsAssembly("Brigade.Admin.Data.Sqlite"))
            .Options;
        return new AuthDbContext(options);
    }
}
