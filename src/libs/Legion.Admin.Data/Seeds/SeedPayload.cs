using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Legion.Admin.Data.Models.Providers;
using Legion.Admin.Data.Seeds.Dtos;

namespace Legion.Admin.Data.Seeds;

public class SeedPayload
{
    public List<SecretOptions> Secrets { get; } = [];
    public List<ProviderOptions> Providers { get; } = [];
    public List<AgentOptions> Agents { get; } = [];
    public List<SeedUserDto> Users { get; } = [];
    public List<OidcApplicationDto> OidcApplications { get; } = [];
    public List<OidcScopeDto> OidcScopes { get; } = [];
}
