using Legion.Admin.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Legion.Admin.Data.PostgreSQL;

public class PostgreSqlDesignTimeFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=localhost;Database=legion-admin;Username=postgres",
                o => o.MigrationsAssembly("Legion.Admin.Data.PostgreSQL"))
            .Options;
        return new AppDbContext(options);
    }
}
