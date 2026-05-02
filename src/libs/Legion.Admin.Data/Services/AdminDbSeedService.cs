using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Legion.Admin.Data.Seeds;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Legion.Admin.Data.Services;

public class AdminDbSeedService(
    ILogger<AdminDbSeedService> logger,
    IServiceProvider serviceProvider,
    IWebHostEnvironment env,
    IConfiguration configuration,
    YamlSeedLoader loader) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (env.EnvironmentName != "Development") return;
        if (configuration["Seeding:Source"] == "Legacy") return;

        var seedPath = ResolveSeedPath();
        var payload = loader.LoadAll(seedPath);

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        foreach (var dto in payload.Agents)
        {
            if (!db.Set<AgentOptions>().Any(a => a.Name == dto.Name))
                db.Set<AgentOptions>().Add(new AgentOptions
                {
                    Id = AgentOptionsId.New(),
                    Name = dto.Name,
                    Description = dto.Description
                });
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private string ResolveSeedPath()
    {
        var configured = configuration["Seeding:Path"] ?? "seed";
        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);
    }
}
