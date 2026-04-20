using Microsoft.Agents.AI;

namespace Brigade.Agents.Providers;

public sealed class AgentFactory
{
    public AIAgent CreateAgent(AgentOptions options) => options.Provider switch
    {
        ProvidersEnum.MiniMax   => new MiniMaxProvider().CreateAgent(options),
        ProvidersEnum.Anthropic => new AnthropicProvider().CreateAgent(options),
        _ => throw new NotSupportedException($"The provider {options.Provider} is not supported.")
    };
}
