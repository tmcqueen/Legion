using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Legion.Admin.Data.Models.Providers;
using Legion.Admin.Data.Seeds.Dtos;

namespace Legion.Admin.Data.Seeds;

public static class SeedEntityRegistry
{
    // YAML `seedType` value -> CLR type. Adding a new seedable type means adding one line here.
    public static readonly Dictionary<string, Type> Map = new()
    {
        ["secret"] = typeof(SecretOptions),
        ["provider"] = typeof(ProviderOptions),
        ["agent"] = typeof(AgentOptions),
        ["user"] = typeof(SeedUserDto),
        ["oidc-application"] = typeof(OidcApplicationDto),
        ["oidc-scope"] = typeof(OidcScopeDto),
    };
}
