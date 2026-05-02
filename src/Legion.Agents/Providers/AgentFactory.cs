using Legion.Admin.Data.Services;
using Microsoft.Agents.AI;

namespace Legion.Agents.Providers;

public sealed class AgentFactory(ISecretsManager secrets)
{
    public async Task<AIAgent> CreateAgentAsync(AgentOptions options, CancellationToken ct = default)
    {
        if (secrets.IsSecretReference(options.ApiKey))
        {
            options = options with
            {
                ApiKey = await secrets.ResolveAsync(
                    new SecretRequest { Path = options.ApiKey! }, ct)
            };
        }

        return Enum.Parse<ProvidersEnum>(options.Provider ?? "UNSUPPORTED") switch
        {
            ProvidersEnum.MiniMax   => new MiniMaxProvider().CreateAgent(options),
            ProvidersEnum.Anthropic => new AnthropicProvider().CreateAgent(options),
            _ => throw new NotSupportedException($"The provider {options.Provider} is not supported.")
        };
    }
}
