using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Legion.Admin.Data.Seeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legion.Admin.Data.Tests.Seeds;

public class YamlSeedIntegrationTests
{
    private static string WriteSeedFolder()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);

        File.WriteAllText(Path.Combine(dir, "agents.yml"), """
            agents:
              - name: Default Agent
                description: The default Legion agent
              - name: Second Agent
                description: Another agent
            """);

        return dir;
    }

    [Fact]
    public async Task SeedAgents_WritesToInMemoryDatabase_ExpectedCount()
    {
        var seedDir = WriteSeedFolder();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        var loader = new YamlSeedLoader(configuration, NullLogger<YamlSeedLoader>.Instance);
        var payload = loader.LoadAll(seedDir);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        await using var db = new AppDbContext(options);

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
        await db.SaveChangesAsync();

        var agentCount = await db.Set<AgentOptions>().CountAsync();
        Assert.Equal(2, agentCount);

        var names = await db.Set<AgentOptions>().Select(a => a.Name).ToListAsync();
        Assert.Contains("Default Agent", names);
        Assert.Contains("Second Agent", names);
    }

    [Fact]
    public async Task SeedAgents_Idempotent_DoesNotDuplicateOnSecondRun()
    {
        var seedDir = WriteSeedFolder();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([])
            .Build();

        var loader = new YamlSeedLoader(configuration, NullLogger<YamlSeedLoader>.Instance);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        // First run
        await using (var db = new AppDbContext(options))
        {
            var payload = loader.LoadAll(seedDir);
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
            await db.SaveChangesAsync();
        }

        // Second run (same DB)
        await using (var db = new AppDbContext(options))
        {
            var payload = loader.LoadAll(seedDir);
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
            await db.SaveChangesAsync();

            var agentCount = await db.Set<AgentOptions>().CountAsync();
            Assert.Equal(2, agentCount);  // still 2, not 4
        }
    }
}
