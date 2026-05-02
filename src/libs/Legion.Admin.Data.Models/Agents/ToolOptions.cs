namespace Legion.Admin.Data.Models.Agents;

public record ToolOptions
{
    public ToolOptionsId Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? ParametersSchema { get; init; }
    public List<AgentOptions> Agents { get; set; } = [];
}
