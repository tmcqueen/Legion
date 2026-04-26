using Brigade.Admin.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Brigade.Admin.Data.Sqlite.Migrations;

public class SqliteDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite("Data Source=brigade-admin.db",
                o => o.MigrationsAssembly("Brigade.Admin.Data.Sqlite"))
            .Options;
        return new AppDbContext(options);
    }
}
