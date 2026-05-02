using Brigade.Admin.Data.Models.Providers;

namespace Brigade.Admin.Data.Models.Agents;

public record AgentOptions
{
    public AgentOptionsId Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Instructions { get; init; }
    public int? MaxTokens { get; init; }
    public ProviderOptionsId ProviderId { get; set; }
    public ProviderOptions? Provider { get; set; }
    public MemoryOptions? Memory { get; set; }
    public List<ModelOptions> Models { get; set; } = [];
    public List<SkillOptions> Skills { get; set; } = [];
    public List<ToolOptions> Tools { get; set; } = [];
    public List<McpServerOptions> McpServers { get; set; } = [];
    public List<MiddlewareOptions> Middleware { get; set; } = [];
}
