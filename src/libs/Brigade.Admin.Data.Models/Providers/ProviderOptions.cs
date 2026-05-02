using Brigade.Admin.Data.Models.Agents;

namespace Brigade.Admin.Data.Models.Providers;

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

public record ProviderOptions
{
    public ProviderOptionsId Id { get; init; }
    public string? Name { get; init; }
    public ProviderType Type { get; init; }
    public string? ApiUrl { get; init; }
    public string? ApiToken { get; init; }
    public List<AgentOptions> Agents { get; set; } = [];
    public List<ModelOptions> Models { get; set; } = [];
}
