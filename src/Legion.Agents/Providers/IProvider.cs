using Microsoft.Agents.AI;

namespace Legion.Agents.Providers;

public interface IProvider
{
    string ProviderName { get; }
    IReadOnlyList<string> SupportedModels { get; }
    AIAgent CreateAgent(AgentOptions options);
}
