using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Legion.Admin.Data.Models.Providers;
using Legion.Admin.Data.Seeds;
using Legion.Admin.Data.Stores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
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

        var seedPath = ResolveSeedPath();
        var payload = loader.LoadAll(seedPath);

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var secretsStore = scope.ServiceProvider.GetRequiredService<ISecretsStore>();

        await SeedSecretsAsync(secretsStore, payload, cancellationToken);
        await SeedProvidersAsync(db, secretsStore, payload, cancellationToken);
        await SeedAgentsAsync(db, payload, cancellationToken);

        await db.SaveChangesAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task SeedSecretsAsync(ISecretsStore secretsStore, SeedPayload payload, CancellationToken ct)
    {
        foreach (var secret in payload.Secrets)
        {
            // The YAML field `encryptedValue` is now semantically the plaintext to encrypt:
            // route through ISecretsStore so the configured encryption is actually applied.
            var existing = await secretsStore.FindByPathAsync(secret.Path, ct);
            if (existing is not null) continue;
            await secretsStore.CreateAsync(secret.Path, secret.Description, secret.EncryptedValue, ct);
        }
    }

    private async Task SeedProvidersAsync(AppDbContext db, ISecretsStore secretsStore, SeedPayload payload, CancellationToken ct)
    {
        // Resolve secret paths against persisted Secrets (single authoritative source).
        var allSecrets = await secretsStore.GetAllAsync(ct);
        var secretsByPath = allSecrets.ToDictionary(s => s.Path, s => s.Id);

        foreach (var provider in payload.Providers)
        {
            if (!string.IsNullOrEmpty(provider.ApiTokenSecretPath))
            {
                if (!secretsByPath.TryGetValue(provider.ApiTokenSecretPath, out var secretId))
                    throw new InvalidOperationException(
                        $"Provider '{provider.Name}' references unknown secret path " +
                        $"'{provider.ApiTokenSecretPath}'. Define it in seed YAML before referencing.");
                provider.ApiTokenSecretId = secretId;
            }

            if (await db.Providers.AnyAsync(p => p.Name == provider.Name, ct)) continue;

            var copy = provider with
            {
                Id = provider.Id == default ? ProviderOptionsId.New() : provider.Id,
                ApiTokenSecretPath = null,  // not persisted, but blank it for clarity
                Models = [],
                Agents = [],
            };
            db.Providers.Add(copy);
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task SeedAgentsAsync(AppDbContext db, SeedPayload payload, CancellationToken ct)
    {
        var providersByName = await db.Providers.AsNoTracking()
            .Where(p => p.Name != null)
            .ToDictionaryAsync(p => p.Name!, p => p.Id, ct);

        foreach (var agent in payload.Agents)
        {
            if (!string.IsNullOrEmpty(agent.ProviderName))
            {
                if (!providersByName.TryGetValue(agent.ProviderName, out var providerId))
                    throw new InvalidOperationException(
                        $"Agent '{agent.Name}' references unknown provider " +
                        $"'{agent.ProviderName}'. Define it in seed YAML before referencing.");
                agent.ProviderId = providerId;
            }

            if (await db.Agents.AnyAsync(a => a.Name == agent.Name, ct)) continue;

            var copy = agent with
            {
                Id = agent.Id == default ? AgentOptionsId.New() : agent.Id,
                ProviderName = null,
                Models = [],
                Skills = [],
                Tools = [],
                McpServers = [],
                Middleware = [],
            };
            db.Agents.Add(copy);
        }
        await db.SaveChangesAsync(ct);
    }

    private string ResolveSeedPath()
    {
        var configured = configuration["Seeding:Path"] ?? "seed";
        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(env.ContentRootPath, configured);
    }
}
