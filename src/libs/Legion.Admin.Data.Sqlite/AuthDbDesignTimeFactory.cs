using Legion.Admin.Data.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Legion.Admin.Data.Sqlite;

public class AuthDbDesignTimeFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlite("Data Source=legion.db",
                o => o.MigrationsAssembly("Legion.Admin.Data.Sqlite"))
            .Options;
        return new AuthDbContext(options);
    }
}
