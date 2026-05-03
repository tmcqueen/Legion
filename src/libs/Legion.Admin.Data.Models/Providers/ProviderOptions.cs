using System.ComponentModel.DataAnnotations.Schema;
using Legion.Admin.Data.Models.Agents;
using Legion.Admin.Data.Seeds;

namespace Legion.Admin.Data.Models.Providers;

public enum ProviderType
{
    Anthropic,
    Ollama,
    OpenAI,
    AzureOpenAI,
    MicrosoftFoundry,
    HuggingFace,
    GithubCopilot,
    CopilotStudio,
    Cloudflare,
    Custom
}

public record ProviderOptions : ISeedEntity
{
    public ProviderOptionsId Id { get; init; }
    public string? Name { get; init; }
    public ProviderType Type { get; init; }
    public string? ApiUrl { get; init; }

    public SecretOptionsId? ApiTokenSecretId { get; set; }
    public SecretOptions? ApiTokenSecret { get; set; }

    // Seed-only linking field. Populated from YAML; resolved to ApiTokenSecretId by
    // AdminDbSeedService and not persisted to the database.
    [NotMapped]
    public string? ApiTokenSecretPath { get; set; }

    public List<AgentOptions> Agents { get; set; } = [];
    public List<ModelOptions> Models { get; set; } = [];
}
