using Legion.Admin.Data.Models;
using Legion.Admin.Data.Seeds;
using Legion.Admin.Data.Services;
using Legion.Admin.Data.Stores;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legion.Admin.Data.Tests.Seeds;

public class AdminDbSeedServiceTests
{
    private sealed class FakeEnv : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";
        public string ApplicationName { get; set; } = "Test";
        public string ContentRootPath { get; set; } = "";
        public string WebRootPath { get; set; } = "";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = null!;
        public Microsoft.Extensions.FileProviders.IFileProvider WebRootFileProvider { get; set; } = null!;
    }

    // Plain-storage ISecretsStore for tests — uses the AppDbContext directly with no
    // backend-specific encryption, mirroring SqliteSecretsStore's behavior closely enough
    // to exercise the seed flow against EF Core InMemory.
    private sealed class FakeSecretsStore(AppDbContext db) : ISecretsStore
    {
        public async Task<List<SecretOptions>> GetAllAsync(CancellationToken ct = default) =>
            await db.Secrets.AsNoTracking().OrderBy(s => s.Path).ToListAsync(ct);

        public async Task<SecretOptions?> FindByPathAsync(string path, CancellationToken ct = default) =>
            await db.Secrets.AsNoTracking().FirstOrDefaultAsync(s => s.Path == path, ct);

        public Task<List<SecretOptions>> GetChildrenAsync(string parentPath, CancellationToken ct = default) =>
            Task.FromResult(new List<SecretOptions>());

        public async Task<SecretOptions> CreateAsync(string path, string? description, string plaintext, CancellationToken ct = default)
        {
            var secret = new SecretOptions
            {
                Id = SecretOptionsId.New(),
                Path = path,
                Description = description,
                EncryptedValue = plaintext,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            db.Secrets.Add(secret);
            await db.SaveChangesAsync(ct);
            return secret;
        }

        public Task UpdateValueAsync(Guid id, string plaintext, CancellationToken ct = default) => Task.CompletedTask;
        public Task UpdateDescriptionAsync(Guid id, string? description, CancellationToken ct = default) => Task.CompletedTask;
        public Task<string?> DecryptAsync(Guid id, CancellationToken ct = default) => Task.FromResult<string?>(null);
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    private static (IServiceProvider sp, AppDbContext db, FakeEnv env) Build(string seedDir)
    {
        var dbName = $"test_{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddScoped<ISecretsStore, FakeSecretsStore>();
        var sp = services.BuildServiceProvider();
        var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();

        return (sp, db, new FakeEnv { ContentRootPath = seedDir });
    }

    private static string WriteSeed(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(Path.Combine(dir, "seed"));
        File.WriteAllText(Path.Combine(dir, "seed", "all.yml"), content);
        return dir;
    }

    [Fact]
    public async Task StartAsync_LinksProviderToSecretByPath()
    {
        var contentRoot = WriteSeed("""
            entities:
              - seedType: secret
                path: providers/x/key
                encryptedValue: literal-value
              - seedType: provider
                name: X
                type: Custom
                apiTokenSecretPath: providers/x/key
            """);
        var (sp, db, env) = Build(contentRoot);

        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var loader = new YamlSeedLoader(config, NullLogger<YamlSeedLoader>.Instance);
        var service = new AdminDbSeedService(NullLogger<AdminDbSeedService>.Instance, sp, env, config, loader);

        await service.StartAsync(CancellationToken.None);

        var provider = await db.Providers.AsNoTracking().Include(p => p.ApiTokenSecret)
            .FirstAsync(p => p.Name == "X");
        Assert.NotNull(provider.ApiTokenSecretId);
        Assert.NotNull(provider.ApiTokenSecret);
        Assert.Equal("providers/x/key", provider.ApiTokenSecret!.Path);
        Assert.Equal("literal-value", provider.ApiTokenSecret.EncryptedValue);
    }

    [Fact]
    public async Task StartAsync_DanglingSecretPath_Throws()
    {
        var contentRoot = WriteSeed("""
            entities:
              - seedType: provider
                name: X
                type: Custom
                apiTokenSecretPath: providers/missing/key
            """);
        var (sp, db, env) = Build(contentRoot);

        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var loader = new YamlSeedLoader(config, NullLogger<YamlSeedLoader>.Instance);
        var service = new AdminDbSeedService(NullLogger<AdminDbSeedService>.Instance, sp, env, config, loader);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StartAsync(CancellationToken.None));
        Assert.Contains("providers/missing/key", ex.Message);
    }

    [Fact]
    public async Task StartAsync_LinksAgentToProviderByName()
    {
        var contentRoot = WriteSeed("""
            entities:
              - seedType: provider
                name: X
                type: Custom
              - seedType: agent
                name: A
                providerName: X
            """);
        var (sp, db, env) = Build(contentRoot);

        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var loader = new YamlSeedLoader(config, NullLogger<YamlSeedLoader>.Instance);
        var service = new AdminDbSeedService(NullLogger<AdminDbSeedService>.Instance, sp, env, config, loader);

        await service.StartAsync(CancellationToken.None);

        var agent = await db.Agents.AsNoTracking().FirstAsync(a => a.Name == "A");
        var provider = await db.Providers.AsNoTracking().FirstAsync(p => p.Name == "X");
        Assert.Equal(provider.Id, agent.ProviderId);
    }
}
