using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Prompts;
using Legion.Agents.Providers;
using Xunit;

namespace Legion.Admin.Data.Tests.Prompts;

public class AgentFactoryPromptTests
{
    private static PromptVersion MakeVersion(string path, PromptCategory category, string content)
    {
        var defId = PromptDefinitionId.New();
        return new PromptVersion
        {
            Id = PromptVersionId.New(),
            DefinitionId = defId,
            Status = PromptStatus.Published,
            Content = content,
            CreatedBy = "test:User",
            Definition = new PromptDefinition
            {
                Id = defId,
                Path = path,
                Type = PromptType.Prompt,
                Category = category,
                CreatedBy = "test:User"
            }
        };
    }

    // ── Tool filtering ─────────────────────────────────────────

    [Fact]
    public void AssembleOptions_ToolWhitelistNull_AllToolsAllowed()
    {
        var options = new AgentOptions { Tools = ["bash", "read", "write"], ToolWhitelist = null };
        var result = AgentFactory.AssembleOptions(options, []);
        Assert.Equal(["bash", "read", "write"], result.Tools);
    }

    [Fact]
    public void AssembleOptions_ToolWhitelistEmpty_AllToolsAllowed()
    {
        var options = new AgentOptions { Tools = ["bash", "read", "write"], ToolWhitelist = [] };
        var result = AgentFactory.AssembleOptions(options, []);
        Assert.Equal(["bash", "read", "write"], result.Tools);
    }

    [Fact]
    public void AssembleOptions_ToolWhitelist_FiltersToWhitelistedOnly()
    {
        var options = new AgentOptions
        {
            Tools = ["bash", "read", "write"],
            ToolWhitelist = ["bash", "read"]
        };
        var result = AgentFactory.AssembleOptions(options, []);
        Assert.Equal(["bash", "read"], result.Tools);
    }

    [Fact]
    public void AssembleOptions_ToolBlacklist_RemovesBlacklistedTools()
    {
        var options = new AgentOptions
        {
            Tools = ["bash", "read", "write"],
            ToolBlacklist = ["write"]
        };
        var result = AgentFactory.AssembleOptions(options, []);
        Assert.DoesNotContain("write", result.Tools!);
        Assert.Contains("bash", result.Tools!);
        Assert.Contains("read", result.Tools!);
    }

    [Fact]
    public void AssembleOptions_BlacklistOverridesWhitelist()
    {
        var options = new AgentOptions
        {
            Tools = ["bash", "read", "write"],
            ToolWhitelist = ["bash", "write"],
            ToolBlacklist = ["write"]
        };
        var result = AgentFactory.AssembleOptions(options, []);
        Assert.Equal(["bash"], result.Tools);
    }

    // ── Prompt assembly ────────────────────────────────────────

    [Fact]
    public void AssembleOptions_NoPrompts_InstructionsUnchanged()
    {
        var options = new AgentOptions { Instructions = "original" };
        var result = AgentFactory.AssembleOptions(options, []);
        Assert.Equal("original", result.Instructions);
    }

    [Fact]
    public void AssembleOptions_NoPrompts_NullInstructionsRemainsNull()
    {
        var options = new AgentOptions { Instructions = null };
        var result = AgentFactory.AssembleOptions(options, []);
        Assert.Null(result.Instructions);
    }

    [Fact]
    public void AssembleOptions_OnePrompt_InstructionsContainsContentAndMarker()
    {
        var prompt = MakeVersion("/System/Identity", PromptCategory.Foundation, "You are an AI.");
        var options = new AgentOptions { Instructions = null };
        var result = AgentFactory.AssembleOptions(options, [prompt]);

        Assert.Contains("You are an AI.", result.Instructions);
        Assert.Contains("<!-- prompt: /System/Identity", result.Instructions);
    }

    [Fact]
    public void AssembleOptions_InlineInstructions_AppendedAfterAssembledPrompts()
    {
        var prompt = MakeVersion("/System/Identity", PromptCategory.Foundation, "You are an AI.");
        var options = new AgentOptions { Instructions = "My custom override." };
        var result = AgentFactory.AssembleOptions(options, [prompt]);

        var promptIdx = result.Instructions!.IndexOf("You are an AI.");
        var overrideIdx = result.Instructions.IndexOf("My custom override.");
        Assert.True(promptIdx < overrideIdx, "Assembled prompts should appear before inline instructions.");
    }

    [Fact]
    public void AssembleOptions_MultiplePrompts_AllMarkersAndContentPresent()
    {
        var p1 = MakeVersion("/System/Identity", PromptCategory.Foundation, "Content A");
        var p2 = MakeVersion("/Rules/Security", PromptCategory.Constraints, "Content B");
        var options = new AgentOptions();
        var result = AgentFactory.AssembleOptions(options, [p1, p2]);

        Assert.Contains("<!-- prompt: /System/Identity", result.Instructions);
        Assert.Contains("<!-- prompt: /Rules/Security", result.Instructions);
        Assert.Contains("Content A", result.Instructions);
        Assert.Contains("Content B", result.Instructions);
    }

    [Fact]
    public void AssembleOptions_NullTools_ToolsRemainsNull()
    {
        var options = new AgentOptions { Tools = null };
        var result = AgentFactory.AssembleOptions(options, []);
        Assert.Null(result.Tools);
    }
}
