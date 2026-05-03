using Legion.Admin.Data.Models.Providers;
using Legion.Admin.Data.Seeds;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Legion.Admin.Data.Tests.Seeds;

public class YamlSeedLoaderTests
{
    private static YamlSeedLoader BuildLoader(Dictionary<string, string?>? config = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(config ?? [])
            .Build();
        return new YamlSeedLoader(configuration, NullLogger<YamlSeedLoader>.Instance);
    }

    private static string WriteTempYaml(string content)
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "seed.yml"), content);
        return dir;
    }

    [Fact]
    public void LoadAll_MissingFolder_ReturnsEmptyPayload()
    {
        var payload = BuildLoader().LoadAll("/nonexistent/path");
        Assert.Empty(payload.Secrets);
        Assert.Empty(payload.Providers);
        Assert.Empty(payload.Agents);
        Assert.Empty(payload.Users);
    }

    [Fact]
    public void LoadAll_AgentEntity_ParsesAgent()
    {
        var dir = WriteTempYaml("""
            entities:
              - seedType: agent
                name: My Agent
                description: A test agent
            """);

        var payload = BuildLoader().LoadAll(dir);

        Assert.Single(payload.Agents);
        Assert.Equal("My Agent", payload.Agents[0].Name);
        Assert.Equal("A test agent", payload.Agents[0].Description);
    }

    [Fact]
    public void LoadAll_SecretEntity_ParsesSecret()
    {
        var dir = WriteTempYaml("""
            entities:
              - seedType: secret
                path: providers/test/key
                description: Test secret
                encryptedValue: literal-value
            """);

        var payload = BuildLoader().LoadAll(dir);

        Assert.Single(payload.Secrets);
        Assert.Equal("providers/test/key", payload.Secrets[0].Path);
        Assert.Equal("literal-value", payload.Secrets[0].EncryptedValue);
    }

    [Fact]
    public void LoadAll_ProviderEntity_PopulatesApiTokenSecretPath()
    {
        var dir = WriteTempYaml("""
            entities:
              - seedType: provider
                name: Anthropic
                type: Anthropic
                apiUrl: https://api.anthropic.com
                apiTokenSecretPath: providers/anthropic/api-token
            """);

        var payload = BuildLoader().LoadAll(dir);

        var p = Assert.Single(payload.Providers);
        Assert.Equal("Anthropic", p.Name);
        Assert.Equal(ProviderType.Anthropic, p.Type);
        Assert.Equal("providers/anthropic/api-token", p.ApiTokenSecretPath);
        Assert.Null(p.ApiTokenSecretId);
    }

    [Fact]
    public void LoadAll_InterpolatesConfigPlaceholder()
    {
        var dir = WriteTempYaml("""
            entities:
              - seedType: oidc-scope
                name: ${MyConfig:ScopeName}
                resources: []
            """);

        var payload = BuildLoader(new Dictionary<string, string?>
        {
            ["MyConfig:ScopeName"] = "my-scope"
        }).LoadAll(dir);

        Assert.Single(payload.OidcScopes);
        Assert.Equal("my-scope", payload.OidcScopes[0].Name);
    }

    [Fact]
    public void LoadAll_UnresolvedPlaceholderInSensitiveField_Throws()
    {
        var dir = WriteTempYaml("""
            entities:
              - seedType: user
                userName: admin
                email: admin@legion.local
                emailConfirmed: true
                password: "${Seeding:Missing}"
            """);

        var ex = Assert.Throws<InvalidOperationException>(() => BuildLoader().LoadAll(dir));
        Assert.Contains("password", ex.Message);
        Assert.Contains("unresolved placeholder", ex.Message);
    }

    [Fact]
    public void LoadAll_UnresolvedPlaceholderInSecretEncryptedValue_Throws()
    {
        var dir = WriteTempYaml("""
            entities:
              - seedType: secret
                path: providers/x/key
                encryptedValue: "${Seeding:Missing}"
            """);

        var ex = Assert.Throws<InvalidOperationException>(() => BuildLoader().LoadAll(dir));
        Assert.Contains("encryptedValue", ex.Message);
    }

    [Fact]
    public void LoadAll_DuplicateProviderName_LogsAndSkips()
    {
        var dir = WriteTempYaml("""
            entities:
              - seedType: provider
                name: Anthropic
                type: Anthropic
              - seedType: provider
                name: Anthropic
                type: Anthropic
            """);

        var payload = BuildLoader().LoadAll(dir);
        Assert.Single(payload.Providers);
    }

    [Fact]
    public void LoadAll_MultipleEntityTypesInOneFile_AllParsed()
    {
        var dir = WriteTempYaml("""
            entities:
              - seedType: secret
                path: providers/x/key
                encryptedValue: v1
              - seedType: provider
                name: X
                type: Custom
                apiTokenSecretPath: providers/x/key
              - seedType: agent
                name: A
                providerName: X
            """);

        var payload = BuildLoader().LoadAll(dir);
        Assert.Single(payload.Secrets);
        Assert.Single(payload.Providers);
        Assert.Single(payload.Agents);
    }
}
