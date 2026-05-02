using Legion.Admin.Data.Models.Agents;

namespace Legion.Admin.Data.Models.Prompts;

public record AgentPromptAssignment
{
    public AgentPromptAssignmentId Id { get; init; } = AgentPromptAssignmentId.New();
    public AgentOptionsId AgentId { get; init; }
    public PromptDefinitionId DefinitionId { get; init; }
    public int Order { get; set; }

    public PromptDefinition? Definition { get; init; }
}
