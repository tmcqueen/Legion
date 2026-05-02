using Legion.Admin.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Legion.Admin.Data.Sqlite;

public class AppDbDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=legion.db",
                o => o.MigrationsAssembly("Legion.Admin.Data.Sqlite"))
            .Options;
        return new AppDbContext(options);
    }
}
