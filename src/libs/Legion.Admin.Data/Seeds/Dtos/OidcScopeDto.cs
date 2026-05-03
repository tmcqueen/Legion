using OpenIddict.Abstractions;

namespace Legion.Admin.Data.Seeds.Dtos;

public record OidcScopeDto : ISeedEntity
{
    public string Name { get; init; } = "";
    public List<string> Resources { get; init; } = [];

    public OpenIddictScopeDescriptor ToDescriptor()
    {
        var descriptor = new OpenIddictScopeDescriptor { Name = Name };
        foreach (var r in Resources)
            descriptor.Resources.Add(r);
        return descriptor;
    }
}
