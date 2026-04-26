using Brigade.Admin.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Brigade.Admin.Data.PostgreSQL.Migrations;

public class PostgreSqlDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=brigade-admin;Username=postgres",
                o => o.MigrationsAssembly("Brigade.Admin.Data.PostgreSQL"))
            .Options;
        return new AppDbContext(options);
    }
}
