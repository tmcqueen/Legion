using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Prompts;
using Legion.Admin.Data.Services;
using Legion.Admin.Data.Stores;
using Microsoft.Agents.AI;

namespace Legion.Agents.Providers;

public sealed class AgentFactory(ISecretsManager secrets, IPromptStore promptStore)
{
    public async Task<AIAgent> CreateAgentAsync(
        AgentOptionsId agentId, AgentOptions options, CancellationToken ct = default)
    {
        if (secrets.IsSecretReference(options.ApiKey))
        {
            options = options with
            {
                ApiKey = await secrets.ResolveAsync(
                    new SecretRequest { Path = options.ApiKey! }, ct)
            };
        }

        var prompts = await promptStore.GetAgentPromptsAsync(agentId, ct);
        options = AssembleOptions(options, prompts);

        return Enum.Parse<ProvidersEnum>(options.Provider ?? "UNSUPPORTED") switch
        {
            ProvidersEnum.MiniMax   => new MiniMaxProvider().CreateAgent(options),
            ProvidersEnum.Anthropic => new AnthropicProvider().CreateAgent(options),
            _ => throw new NotSupportedException($"The provider {options.Provider} is not supported.")
        };
    }

    internal static AgentOptions AssembleOptions(AgentOptions options, IList<PromptVersion> prompts)
    {
        if (prompts.Count > 0)
        {
            var sections = prompts.Select(p =>
                $"<!-- prompt: {p.Definition?.Path ?? p.DefinitionId.ToString()} v={p.Id.Value.ToString("N")[..8]} -->\n{p.Content}");
            var assembled = string.Join("\n\n", sections);
            var inline = options.Instructions ?? string.Empty;
            options = options with
            {
                Instructions = string.IsNullOrEmpty(inline) ? assembled : $"{assembled}\n\n{inline}"
            };
        }

        if (options.Tools is not null)
        {
            var filtered = options.Tools
                .Where(t => options.ToolWhitelist is null or { Count: 0 } || options.ToolWhitelist.Contains(t))
                .Where(t => options.ToolBlacklist is null || !options.ToolBlacklist.Contains(t))
                .ToList();
            options = options with { Tools = filtered };
        }

        return options;
    }
}
