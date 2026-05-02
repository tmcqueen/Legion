namespace Legion.Admin.Data.Models.Prompts;

public class PromptVersion
{
    public PromptVersionId Id { get; init; } = PromptVersionId.New();
    public PromptDefinitionId DefinitionId { get; init; }
    public PromptStatus Status { get; set; }
    public string Content { get; set; } = string.Empty;
    public string? Frontmatter { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string CreatedBy { get; init; } = string.Empty;
    public string? Notes { get; set; }

    public PromptDefinition? Definition { get; init; }
}
