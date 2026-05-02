using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Prompts;
using Legion.Admin.Data.Stores;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Legion.Admin.Data.Tests.Prompts;

public class PromptStoreTests
{
    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new AppDbContext(options);
    }

    // ── CreateDefinitionAsync ───────────────────────────────────

    [Fact]
    public async Task CreateDefinitionAsync_ValidPath_Succeeds()
    {
        await using var db = CreateDb();
        var store = new PromptStore(db);
        var def = await store.CreateDefinitionAsync(
            "/Agents/Prompts/Bash", PromptType.Prompt, PromptCategory.TaskSpecific,
            false, "test:Test User");

        Assert.Equal("/Agents/Prompts/Bash", def.Path);
        Assert.Equal(PromptType.Prompt, def.Type);
        Assert.Single(await db.PromptDefinitions.ToListAsync());
    }

    [Fact]
    public async Task CreateDefinitionAsync_InvalidPath_Throws()
    {
        await using var db = CreateDb();
        var store = new PromptStore(db);
        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.CreateDefinitionAsync(
                "no-leading-slash", PromptType.Prompt, PromptCategory.TaskSpecific,
                false, "test:Test User"));
    }

    [Fact]
    public async Task CreateDefinitionAsync_DuplicatePath_Throws()
    {
        await using var db = CreateDb();
        var store = new PromptStore(db);
        await store.CreateDefinitionAsync("/Rules/Security", PromptType.Prompt, PromptCategory.Constraints, false, "test:User");
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CreateDefinitionAsync("/Rules/Security", PromptType.Prompt, PromptCategory.Constraints, false, "test:User"));
    }

    // ── CreateDraftAsync ───────────────────────────────────────

    [Fact]
    public async Task CreateDraftAsync_Succeeds()
    {
        await using var db = CreateDb();
        var store = new PromptStore(db);
        var def = await store.CreateDefinitionAsync("/Test/Path", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");
        var draft = await store.CreateDraftAsync(def.Id, "# Hello", null, "test:User", null);

        Assert.Equal(PromptStatus.Draft, draft.Status);
        Assert.Equal("# Hello", draft.Content);
    }

    [Fact]
    public async Task CreateDraftAsync_SecondDraftConflicts()
    {
        await using var db = CreateDb();
        var store = new PromptStore(db);
        var def = await store.CreateDefinitionAsync("/Test/Path", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");
        await store.CreateDraftAsync(def.Id, "# First", null, "test:User", null);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.CreateDraftAsync(def.Id, "# Second", null, "test:User", null));
    }

    [Fact]
    public async Task CreateDraftAsync_EmptyContent_Throws()
    {
        await using var db = CreateDb();
        var store = new PromptStore(db);
        var def = await store.CreateDefinitionAsync("/Test/Path", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.CreateDraftAsync(def.Id, "   ", null, "test:User", null));
    }

    [Fact]
    public async Task CreateDraftAsync_InvalidYamlFrontmatter_Throws()
    {
        await using var db = CreateDb();
        var store = new PromptStore(db);
        var def = await store.CreateDefinitionAsync("/Skills/Git", PromptType.Skill, PromptCategory.TaskSpecific, false, "test:User");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            store.CreateDraftAsync(def.Id, "content", "name: [unclosed", "test:User", null));
    }

    // ── PublishDraftAsync ──────────────────────────────────────

    [Fact]
    public async Task PublishDraftAsync_ArchivesCurrentPublished()
    {
        await using var db = CreateDb();
        var store = new PromptStore(db);
        var def = await store.CreateDefinitionAsync("/Test/Path", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");

        var v1 = await store.CreateDraftAsync(def.Id, "# V1", null, "test:User", null);
        await store.PublishDraftAsync(v1.Id);

        var v2 = await store.CreateDraftAsync(def.Id, "# V2", null, "test:User", null);
        await store.PublishDraftAsync(v2.Id);

        var v1Reloaded = await db.PromptVersions.FindAsync([v1.Id]);
        var v2Reloaded = await db.PromptVersions.FindAsync([v2.Id]);

        Assert.Equal(PromptStatus.Archived, v1Reloaded!.Status);
        Assert.Equal(PromptStatus.Published, v2Reloaded!.Status);
    }

    [Fact]
    public async Task PublishDraftAsync_NoPreviousPublished_Succeeds()
    {
        await using var db = CreateDb();
        var store = new PromptStore(db);
        var def = await store.CreateDefinitionAsync("/Test/Path", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");
        var v1 = await store.CreateDraftAsync(def.Id, "# V1", null, "test:User", null);
        await store.PublishDraftAsync(v1.Id);

        var published = await store.GetPublishedPromptAsync("/Test/Path");
        Assert.NotNull(published);
        Assert.Equal("# V1", published.Content);
    }

    // ── RepublishArchivedAsync ─────────────────────────────────

    [Fact]
    public async Task RepublishArchivedAsync_ArchivesCurrentAndPromotesOld()
    {
        await using var db = CreateDb();
        var store = new PromptStore(db);
        var def = await store.CreateDefinitionAsync("/Test/Path", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");
        var v1 = await store.CreateDraftAsync(def.Id, "# V1", null, "test:User", null);
        await store.PublishDraftAsync(v1.Id);
        var v2 = await store.CreateDraftAsync(def.Id, "# V2", null, "test:User", null);
        await store.PublishDraftAsync(v2.Id);

        // v1 is now Archived, v2 is Published. Republish v1.
        await store.RepublishArchivedAsync(v1.Id);

        var v1After = await db.PromptVersions.FindAsync([v1.Id]);
        var v2After = await db.PromptVersions.FindAsync([v2.Id]);
        Assert.Equal(PromptStatus.Published, v1After!.Status);
        Assert.Equal(PromptStatus.Archived, v2After!.Status);
    }

    // ── GetAgentPromptsAsync ───────────────────────────────────

    [Fact]
    public async Task GetAgentPromptsAsync_ReturnsSortedByCategoryThenOrder()
    {
        await using var db = CreateDb();
        var store = new PromptStore(db);
        var agentId = AgentOptionsId.New();

        var d1 = await store.CreateDefinitionAsync("/Rules/Security", PromptType.Prompt, PromptCategory.Constraints, false, "test:User");
        var v1 = await store.CreateDraftAsync(d1.Id, "# Security", null, "test:User", null);
        await store.PublishDraftAsync(v1.Id);

        var d2 = await store.CreateDefinitionAsync("/System/Identity", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");
        var v2 = await store.CreateDraftAsync(d2.Id, "# Identity", null, "test:User", null);
        await store.PublishDraftAsync(v2.Id);

        await store.SetAgentAssignmentsAsync(agentId, [
            (d1.Id, 0),
            (d2.Id, 0)
        ]);

        var prompts = await store.GetAgentPromptsAsync(agentId);
        Assert.Equal(2, prompts.Count);
        Assert.Equal("# Identity", prompts[0].Content); // Foundation (0) < Constraints (1)
        Assert.Equal("# Security", prompts[1].Content);
    }

    // ── DeleteDefinitionAsync ──────────────────────────────────

    [Fact]
    public async Task DeleteDefinitionAsync_SoftDeletes()
    {
        await using var db = CreateDb();
        var store = new PromptStore(db);
        var def = await store.CreateDefinitionAsync("/Test/Path", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");
        await store.DeleteDefinitionAsync(def.Id);

        var reloaded = await db.PromptDefinitions.FindAsync([def.Id]);
        Assert.NotNull(reloaded!.DeletedAt);

        var results = await store.SearchDefinitionsAsync("Test");
        Assert.Empty(results); // hidden by default

        var withDeleted = await store.SearchDefinitionsAsync("Test", includeDeleted: true);
        Assert.Single(withDeleted);
    }

    // ── UpdateDraftAsync ────────────────────────────────────────

    [Fact]
    public async Task UpdateDraftAsync_NotFound_ThrowsKeyNotFound()
    {
        await using var db = CreateDb();
        var store = new PromptStore(db);
        var fakeId = PromptVersionId.New();
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            store.UpdateDraftAsync(fakeId, "new content", null));
    }

    [Fact]
    public async Task UpdateDraftAsync_PublishedVersion_ThrowsInvalidOperation()
    {
        await using var db = CreateDb();
        var store = new PromptStore(db);
        var def = await store.CreateDefinitionAsync("/Test/Path", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");
        var v1 = await store.CreateDraftAsync(def.Id, "# Original", null, "test:User", null);
        await store.PublishDraftAsync(v1.Id);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            store.UpdateDraftAsync(v1.Id, "# Modified", null));
    }

    // ── DiscardDraftAsync ──────────────────────────────────────

    [Fact]
    public async Task DiscardDraftAsync_RemovesDraft()
    {
        await using var db = CreateDb();
        var store = new PromptStore(db);
        var def = await store.CreateDefinitionAsync("/Test/Path", PromptType.Prompt, PromptCategory.Foundation, false, "test:User");
        var draft = await store.CreateDraftAsync(def.Id, "# Draft", null, "test:User", null);

        await store.DiscardDraftAsync(draft.Id);

        Assert.Empty(await db.PromptVersions.ToListAsync());
    }
}
