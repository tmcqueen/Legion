using Legion.Admin.Data.Seeds.Dtos;

namespace Legion.Admin.Data.Seeds;

public class SeedPayload
{
    public List<SeedAgentDto> Agents { get; } = [];
    public List<SeedUserDto> Users { get; } = [];
    public List<OidcApplicationDto> OidcApplications { get; } = [];
    public List<OidcScopeDto> OidcScopes { get; } = [];
}
