using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Prompts;

namespace Legion.Admin.Data.Stores;

public interface IPromptStore
{
    Task<PromptVersion?> GetPublishedPromptAsync(string path, CancellationToken ct = default);
    Task<PromptVersion?> GetPromptVersionAsync(PromptVersionId id, CancellationToken ct = default);
    Task<List<PromptVersion>> GetAgentPromptsAsync(AgentOptionsId agentId, CancellationToken ct = default);
    Task<List<PromptVersion>> GetPromptHistoryAsync(PromptDefinitionId definitionId, CancellationToken ct = default);
    Task<List<PromptDefinition>> SearchDefinitionsAsync(string query, PromptType? typeFilter = null, bool includeDeleted = false, CancellationToken ct = default);
    Task<PromptDefinition> CreateDefinitionAsync(string path, PromptType type, PromptCategory category, bool isDefaultIncluded, string createdBy, CancellationToken ct = default);
    Task<PromptVersion> CreateDraftAsync(PromptDefinitionId definitionId, string content, string? frontmatter, string createdBy, string? notes, CancellationToken ct = default);
    Task UpdateDraftAsync(PromptVersionId draftId, string content, string? frontmatter, CancellationToken ct = default);
    Task PublishDraftAsync(PromptVersionId draftId, CancellationToken ct = default);
    Task DiscardDraftAsync(PromptVersionId draftId, CancellationToken ct = default);
    Task RepublishArchivedAsync(PromptVersionId archivedVersionId, CancellationToken ct = default);
    Task DeleteDefinitionAsync(PromptDefinitionId definitionId, CancellationToken ct = default);
    Task<List<AgentPromptAssignment>> GetAgentAssignmentsAsync(AgentOptionsId agentId, CancellationToken ct = default);
    Task SetAgentAssignmentsAsync(AgentOptionsId agentId, IEnumerable<(PromptDefinitionId definitionId, int order)> assignments, CancellationToken ct = default);
}
