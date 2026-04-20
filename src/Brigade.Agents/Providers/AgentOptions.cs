using Microsoft.Extensions.AI;

namespace Brigade.Agents.Providers;

public record AgentOptions
{
    public ProvidersEnum Provider { get; init; }
    public string? ApiKey { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Instructions { get; init; }
    public string? Model { get; init; }
    public List<string>? Tools { get; init; }
    public int? MaxTokens { get; init; }
}
