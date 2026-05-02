using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Prompts;
using Microsoft.EntityFrameworkCore;
using YamlDotNet.Core;

namespace Legion.Admin.Data.Stores;

public class PromptStore(AppDbContext db) : IPromptStore
{
    private static readonly System.Text.RegularExpressions.Regex PathRegex =
        new(@"^(/[\w\s\-]+)+$", System.Text.RegularExpressions.RegexOptions.Compiled);

    public async Task<PromptVersion?> GetPublishedPromptAsync(string path, CancellationToken ct = default) =>
        await db.PromptVersions.AsNoTracking()
            .Include(v => v.Definition)
            .Where(v => v.Definition!.Path == path
                     && v.Status == PromptStatus.Published
                     && v.Definition.DeletedAt == null)
            .FirstOrDefaultAsync(ct);

    public async Task<PromptVersion?> GetPromptVersionAsync(PromptVersionId id, CancellationToken ct = default) =>
        await db.PromptVersions.AsNoTracking()
            .Include(v => v.Definition)
            .FirstOrDefaultAsync(v => v.Id == id, ct);

    public async Task<List<PromptVersion>> GetAgentPromptsAsync(AgentOptionsId agentId, CancellationToken ct = default)
    {
        var assignments = await db.AgentPromptAssignments.AsNoTracking()
            .Where(a => a.AgentId == agentId && a.Definition!.DeletedAt == null)
            .Include(a => a.Definition)
            .OrderBy(a => a.Definition!.Category)
            .ThenBy(a => a.Order)
            .ToListAsync(ct);

        var definitionIds = assignments.Select(a => a.DefinitionId).ToList();
        var publishedVersions = await db.PromptVersions.AsNoTracking()
            .Include(v => v.Definition)
            .Where(v => definitionIds.Contains(v.DefinitionId) && v.Status == PromptStatus.Published)
            .ToListAsync(ct);

        var versionMap = publishedVersions.ToDictionary(v => v.DefinitionId);
        return assignments
            .Where(a => versionMap.ContainsKey(a.DefinitionId))
            .Select(a => versionMap[a.DefinitionId])
            .ToList();
    }

    public async Task<List<PromptVersion>> GetPromptHistoryAsync(PromptDefinitionId definitionId, CancellationToken ct = default) =>
        await db.PromptVersions.AsNoTracking()
            .Include(v => v.Definition)
            .Where(v => v.DefinitionId == definitionId)
            .OrderByDescending(v => v.CreatedAt)
            .ToListAsync(ct);

    public async Task<List<PromptDefinition>> SearchDefinitionsAsync(
        string query, PromptType? typeFilter = null, bool includeDeleted = false, CancellationToken ct = default)
    {
        var q = db.PromptDefinitions.AsNoTracking().AsQueryable();
        if (!includeDeleted) q = q.Where(d => d.DeletedAt == null);
        if (typeFilter.HasValue) q = q.Where(d => d.Type == typeFilter.Value);
        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(d => d.Path.Contains(query));
        return await q.OrderBy(d => d.Path).ToListAsync(ct);
    }

    public async Task<PromptDefinition> CreateDefinitionAsync(
        string path, PromptType type, PromptCategory category, bool isDefaultIncluded,
        string createdBy, CancellationToken ct = default)
    {
        ValidatePath(path);
        var existing = await db.PromptDefinitions.FirstOrDefaultAsync(d => d.Path == path, ct);
        if (existing is not null)
            throw new InvalidOperationException($"A prompt definition with path '{path}' already exists.");

        var definition = new PromptDefinition
        {
            Path = path,
            Type = type,
            Category = category,
            IsDefaultIncluded = isDefaultIncluded,
            CreatedBy = createdBy
        };
        db.PromptDefinitions.Add(definition);
        await db.SaveChangesAsync(ct);
        return definition;
    }

    public async Task<PromptVersion> CreateDraftAsync(
        PromptDefinitionId definitionId, string content, string? frontmatter,
        string createdBy, string? notes, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        var definition = await db.PromptDefinitions.FindAsync([definitionId], ct)
            ?? throw new KeyNotFoundException($"Definition {definitionId} not found.");

        if (frontmatter is not null && definition.Type != PromptType.Prompt)
            ValidateYaml(frontmatter);

        var existingDraft = await db.PromptVersions
            .AnyAsync(v => v.DefinitionId == definitionId && v.Status == PromptStatus.Draft, ct);
        if (existingDraft)
            throw new InvalidOperationException(
                $"Definition {definitionId} already has a Draft. Discard it before creating a new draft.");

        var version = new PromptVersion
        {
            DefinitionId = definitionId,
            Status = PromptStatus.Draft,
            Content = content,
            Frontmatter = frontmatter,
            CreatedBy = createdBy,
            Notes = notes
        };
        db.PromptVersions.Add(version);
        await db.SaveChangesAsync(ct);
        return version;
    }

    public async Task UpdateDraftAsync(
        PromptVersionId draftId, string content, string? frontmatter, CancellationToken ct = default)
    {
        var draft = await db.PromptVersions
            .Include(v => v.Definition)
            .FirstOrDefaultAsync(v => v.Id == draftId, ct);
        if (draft is null)
            throw new KeyNotFoundException($"Draft {draftId} not found.");
        if (draft.Status != PromptStatus.Draft)
            throw new InvalidOperationException($"Version {draftId} is not a Draft (status: {draft.Status}).");

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty.", nameof(content));

        if (frontmatter is not null && draft.Definition is not null && draft.Definition.Type != PromptType.Prompt)
            ValidateYaml(frontmatter);

        draft.Content = content;
        draft.Frontmatter = frontmatter;
        await db.SaveChangesAsync(ct);
    }

    public async Task PublishDraftAsync(PromptVersionId draftId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);

        var draft = await db.PromptVersions.FindAsync([draftId], ct)
            ?? throw new KeyNotFoundException($"Draft {draftId} not found.");
        if (draft.Status != PromptStatus.Draft)
            throw new InvalidOperationException($"Version {draftId} is not a Draft.");

        var currentPublished = await db.PromptVersions
            .Where(v => v.DefinitionId == draft.DefinitionId && v.Status == PromptStatus.Published)
            .FirstOrDefaultAsync(ct);

        if (currentPublished is not null)
            currentPublished.Status = PromptStatus.Archived;

        draft.Status = PromptStatus.Published;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task DiscardDraftAsync(PromptVersionId draftId, CancellationToken ct = default)
    {
        var draft = await db.PromptVersions.FindAsync([draftId], ct);
        if (draft is null)
            throw new KeyNotFoundException($"Draft {draftId} not found.");
        if (draft.Status != PromptStatus.Draft)
            throw new InvalidOperationException($"Version {draftId} is not a Draft.");

        db.PromptVersions.Remove(draft);
        await db.SaveChangesAsync(ct);
    }

    public async Task RepublishArchivedAsync(PromptVersionId archivedVersionId, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(
            System.Data.IsolationLevel.Serializable, ct);

        var archived = await db.PromptVersions.FindAsync([archivedVersionId], ct)
            ?? throw new KeyNotFoundException($"Version {archivedVersionId} not found.");
        if (archived.Status != PromptStatus.Archived)
            throw new InvalidOperationException($"Version {archivedVersionId} is not Archived.");

        var currentPublished = await db.PromptVersions
            .Where(v => v.DefinitionId == archived.DefinitionId && v.Status == PromptStatus.Published)
            .FirstOrDefaultAsync(ct);

        if (currentPublished is not null)
            currentPublished.Status = PromptStatus.Archived;

        archived.Status = PromptStatus.Published;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task DeleteDefinitionAsync(PromptDefinitionId definitionId, CancellationToken ct = default)
    {
        var definition = await db.PromptDefinitions.FindAsync([definitionId], ct)
            ?? throw new KeyNotFoundException($"Definition {definitionId} not found.");
        definition.DeletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    public async Task<List<AgentPromptAssignment>> GetAgentAssignmentsAsync(
        AgentOptionsId agentId, CancellationToken ct = default) =>
        await db.AgentPromptAssignments.AsNoTracking()
            .Include(a => a.Definition)
            .Where(a => a.AgentId == agentId)
            .OrderBy(a => a.Definition!.Category)
            .ThenBy(a => a.Order)
            .ToListAsync(ct);

    public async Task SetAgentAssignmentsAsync(
        AgentOptionsId agentId,
        IEnumerable<(PromptDefinitionId definitionId, int order)> assignments,
        CancellationToken ct = default)
    {
        var existing = await db.AgentPromptAssignments
            .Where(a => a.AgentId == agentId)
            .ToListAsync(ct);
        db.AgentPromptAssignments.RemoveRange(existing);

        foreach (var (definitionId, order) in assignments)
        {
            db.AgentPromptAssignments.Add(new AgentPromptAssignment
            {
                AgentId = agentId,
                DefinitionId = definitionId,
                Order = order
            });
        }
        await db.SaveChangesAsync(ct);
    }

    private static void ValidatePath(string path)
    {
        if (!PathRegex.IsMatch(path))
            throw new ArgumentException(
                $"Path '{path}' is invalid. Must match ^(/[\\w\\s\\-]+)+$", nameof(path));
    }

    private static void ValidateYaml(string yaml)
    {
        try
        {
            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder().Build();
            deserializer.Deserialize<object>(yaml);
        }
        catch (YamlException ex)
        {
            throw new ArgumentException($"Frontmatter is not valid YAML: {ex.Message}", nameof(yaml));
        }
    }
}
