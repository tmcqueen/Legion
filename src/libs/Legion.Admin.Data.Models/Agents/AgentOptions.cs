using System.ComponentModel.DataAnnotations.Schema;
using Legion.Admin.Data.Models.Providers;
using Legion.Admin.Data.Seeds;

namespace Legion.Admin.Data.Models.Agents;

public record AgentOptions : ISeedEntity
{
    public AgentOptionsId Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? Instructions { get; init; }
    public int? MaxTokens { get; init; }
    public ProviderOptionsId ProviderId { get; set; }
    public ProviderOptions? Provider { get; set; }

    [NotMapped]
    public string? ProviderName { get; set; }

    public MemoryOptions? Memory { get; set; }
    public List<ModelOptions> Models { get; set; } = [];
    public List<SkillOptions> Skills { get; set; } = [];
    public List<ToolOptions> Tools { get; set; } = [];
    public List<McpServerOptions> McpServers { get; set; } = [];
    public List<MiddlewareOptions> Middleware { get; set; } = [];
}
