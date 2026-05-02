using Microsoft.Extensions.DependencyInjection;
using Legion.Admin.Data.Services;
using Legion.Admin.Data.Stores;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Builder;

namespace Legion.Admin.Data;

public static partial class Extensions
{
    public static IServiceCollection AddInMemoryAgentDbContext(this IServiceCollection services)
    {
        return services.AddDbContext<AppDbContext>(options =>
            options.UseInMemoryDatabase("AgentDb"));
    }

    public static IServiceCollection AddAgentStores(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddScoped<AgentStore>();
        services.AddScoped<ToolStore>();
        services.AddScoped<SkillStore>();
        services.AddScoped<MemoryStore>();
        services.AddScoped<McpStore>();
        services.AddScoped<WorkflowStore>();
        services.AddScoped<ProviderStore>();
        services.AddScoped<ModelStore>();
        services.AddScoped<MiddlewareStore>();
        services.AddScoped<ISecretsManager, SecretsManager>();
        return services;
    }
    public static async Task DoAgentDbMigration(this WebApplication app)
    {
        using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}
