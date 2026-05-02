namespace Legion.Admin.Data.Models.Agents;

public record SkillOptions
{
    public SkillOptionsId Id { get; init; }
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? License { get; init; }
    public string? Compatibility { get; init; }
    public string? AllowedTools { get; init; }
    public string? Content { get; init; }
    public string? Resources { get; init; }
    public string? Scripts { get; init; }
    public List<AgentOptions> Agents { get; set; } = [];
}
