using Brigade.Admin.Data.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Brigade.Admin.Data.PostgreSQL;

public class AuthDbContextDesignTimeFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql("Host=localhost;Database=brigade-admin;Username=postgres",
                o => o.MigrationsAssembly("Brigade.Admin.Data.PostgreSQL"))
            .Options;
        return new AuthDbContext(options);
    }
}
