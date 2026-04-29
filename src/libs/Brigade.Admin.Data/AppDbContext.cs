using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Models.Agents;
using Brigade.Admin.Data.Models.Providers;
using Microsoft.EntityFrameworkCore;

namespace Brigade.Admin.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<AgentOptions> Agents => Set<AgentOptions>();
    public DbSet<ToolOptions> Tools => Set<ToolOptions>();
    public DbSet<SkillOptions> Skills => Set<SkillOptions>();
    public DbSet<MemoryOptions> Memories => Set<MemoryOptions>();
    public DbSet<McpServerOptions> Mcps => Set<McpServerOptions>();
    public DbSet<McpServerHeaders> McpServerHeaders => Set<McpServerHeaders>();
    public DbSet<WorkflowOptions> Workflows => Set<WorkflowOptions>();
    public DbSet<ProviderOptions> Providers => Set<ProviderOptions>();
    public DbSet<ModelOptions> Models => Set<ModelOptions>();
    public DbSet<MiddlewareOptions> Middlewares => Set<MiddlewareOptions>();
    public DbSet<SecretOptions> Secrets => Set<SecretOptions>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
