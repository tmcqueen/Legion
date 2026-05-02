namespace Legion.Admin.Data.Models.Prompts;

public record PromptDefinition
{
    public PromptDefinitionId Id { get; init; } = PromptDefinitionId.New();
    public string Path { get; set; } = string.Empty;
    public PromptType Type { get; set; }
    public PromptCategory Category { get; set; }
    public bool IsDefaultIncluded { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string CreatedBy { get; init; } = string.Empty;
    public DateTime? DeletedAt { get; set; }

    public ICollection<PromptVersion> Versions { get; init; } = [];
    public ICollection<AgentPromptAssignment> Assignments { get; init; } = [];
}
