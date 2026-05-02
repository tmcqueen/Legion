namespace Legion.Admin.Data.Models.Agents;

public record MemoryOptions
{
    public MemoryOptionsId Id { get; init; }

    public AgentOptionsId AgentId { get; set; }
    public AgentOptions? Agent { get; set; }

    public SearchBehavior SearchTime { get; init; } = SearchBehavior.BeforeAIInvoke;
    public int? MaxResults { get; init; }
    public string? FunctionToolName { get; init; }
    public string? FunctionToolDescription { get; init; }
    public string? ContextPrompt { get; init; }
    public string? StateKey { get; init; }
}
