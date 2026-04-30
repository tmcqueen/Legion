using Brigade.Admin.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Brigade.Admin.Data.Sqlite;

public class AppDbDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=brigade.db",
                o => o.MigrationsAssembly("Brigade.Admin.Data.Sqlite"))
            .Options;
        return new AppDbContext(options);
    }
}
