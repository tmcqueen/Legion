using Legion.Admin.Data.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Legion.Admin.Data.PostgreSQL;

public class AuthDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql("Host=localhost;Database=legion-admin;Username=postgres",
                o => o.MigrationsAssembly("Legion.Admin.Data.PostgreSQL"))
            .Options;
        return new AuthDbContext(options);
    }
}
