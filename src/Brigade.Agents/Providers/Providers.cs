using Anthropic;
using Brigade.Agents.Tools;
using Microsoft.Agents.AI;

namespace Brigade.Agents.Providers;

public sealed class AnthropicProvider : IProvider
{
    public string ProviderName => "Anthropic";

    public IReadOnlyList<string> SupportedModels { get; } =
    [
        "claude-opus-4.7",
        "claude-opus-4.6",
        "claude-opus-4.5",
        "claude-sonnet-4.6",
        "claude-sonnet-4.5",
        "claude-haiku-4.5",
    ];

    public AIAgent CreateAgent(AgentOptions options)
        => AnthropicCompatible.Create(options, "https://api.anthropic.com/v1");
}

public sealed class MiniMaxProvider : IProvider
{
    public string ProviderName => "MiniMax";

    public IReadOnlyList<string> SupportedModels { get; } =
    [
        "MiniMax-M2.7",
        "MiniMax-M2.7-highspeed",
        "MiniMax-M2.5",
        "MiniMax-M2.5-highspeed",
        "MiniMax-M2.1",
        "MiniMax-M2.1-highspeed",
        "MiniMax-M2",
    ];

    public AIAgent CreateAgent(AgentOptions options)
        => AnthropicCompatible.Create(options, "https://api.minimax.io/anthropic");
}

internal static class AnthropicCompatible
{
    public static AIAgent Create(AgentOptions options, string baseUrl)
    {
        ArgumentException.ThrowIfNullOrEmpty(options.ApiKey);
        ArgumentException.ThrowIfNullOrEmpty(options.Model);

        var client = new AnthropicClient
        {
            ApiKey = options.ApiKey,
            BaseUrl = baseUrl,
        };

        return client.AsAIAgent(
            model: options.Model,
            instructions: options.Instructions,
            name: options.Name,
            description: options.Description,
            tools: options.Tools?.Select(t => ToolsProvider.GetTool(t)).ToList(),
            defaultMaxTokens: options.MaxTokens);
    }
}
