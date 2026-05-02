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
        var loader = BuildLoader();
        var payload = loader.LoadAll("/nonexistent/path/that/does/not/exist");
        Assert.Empty(payload.Agents);
        Assert.Empty(payload.Users);
        Assert.Empty(payload.OidcApplications);
        Assert.Empty(payload.OidcScopes);
    }

    [Fact]
    public void LoadAll_AgentsYaml_ParsesAgents()
    {
        var dir = WriteTempYaml("""
            agents:
              - name: My Agent
                description: A test agent
            """);

        var loader = BuildLoader();
        var payload = loader.LoadAll(dir);

        Assert.Single(payload.Agents);
        Assert.Equal("My Agent", payload.Agents[0].Name);
        Assert.Equal("A test agent", payload.Agents[0].Description);
    }

    [Fact]
    public void LoadAll_InterpolatesConfigPlaceholder()
    {
        var dir = WriteTempYaml("""
            oidc-scopes:
              - name: ${MyConfig:ScopeName}
                resources: []
            """);

        var loader = BuildLoader(new Dictionary<string, string?>
        {
            ["MyConfig:ScopeName"] = "my-scope"
        });
        var payload = loader.LoadAll(dir);

        Assert.Single(payload.OidcScopes);
        Assert.Equal("my-scope", payload.OidcScopes[0].Name);
    }

    [Fact]
    public void LoadAll_UnresolvedPlaceholderInSensitiveField_Throws()
    {
        var dir = WriteTempYaml("""
            users:
              - userName: admin
                email: admin@legion.local
                emailConfirmed: true
                password: "${Seeding:AdminPassword}"
            """);

        var loader = BuildLoader(); // no config — placeholder stays unresolved
        var ex = Assert.Throws<InvalidOperationException>(() => loader.LoadAll(dir));
        Assert.Contains("unresolved placeholder", ex.Message);
    }

    [Fact]
    public void LoadAll_UnknownTopLevelKey_SkipsWithoutCrash()
    {
        var dir = WriteTempYaml("""
            unknown-entity:
              - foo: bar
            """);

        var loader = BuildLoader();
        var payload = loader.LoadAll(dir);

        Assert.Empty(payload.Agents);
        Assert.Empty(payload.Users);
    }

    [Fact]
    public void LoadAll_MalformedYaml_SkipsFileWithoutCrash()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "bad.yml"), "agents:\n  - name: [unclosed");

        var loader = BuildLoader();
        var payload = loader.LoadAll(dir); // should not throw

        Assert.Empty(payload.Agents);
    }

    [Fact]
    public void LoadAll_DuplicateAgentName_KeepsFirst()
    {
        var dir = WriteTempYaml("""
            agents:
              - name: Same Agent
                description: First
              - name: Same Agent
                description: Second
            """);

        var loader = BuildLoader();
        var payload = loader.LoadAll(dir);

        Assert.Single(payload.Agents);
        Assert.Equal("First", payload.Agents[0].Description);
    }

    [Fact]
    public void LoadAll_FilesLoadedInSortedOrder()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "z-last.yml"), """
            agents:
              - name: Z Agent
            """);
        File.WriteAllText(Path.Combine(dir, "a-first.yml"), """
            agents:
              - name: A Agent
            """);

        var loader = BuildLoader();
        var payload = loader.LoadAll(dir);

        Assert.Equal(2, payload.Agents.Count);
        Assert.Equal("A Agent", payload.Agents[0].Name);
        Assert.Equal("Z Agent", payload.Agents[1].Name);
    }

    [Fact]
    public void LoadAll_BothYmlAndYamlExtensions_AreDiscovered()
    {
        var dir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "a.yml"), """
            agents:
              - name: From YML
            """);
        File.WriteAllText(Path.Combine(dir, "b.yaml"), """
            agents:
              - name: From YAML
            """);

        var loader = BuildLoader();
        var payload = loader.LoadAll(dir);

        Assert.Equal(2, payload.Agents.Count);
    }
}
