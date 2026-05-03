using OpenIddict.Abstractions;

namespace Legion.Admin.Data.Seeds.Dtos;

public record OidcApplicationDto : ISeedEntity
{
    public string ClientId { get; init; } = "";
    public string ClientSecret { get; init; } = "";
    public string ClientType { get; init; } = "confidential";
    public string? ConsentType { get; init; }
    public string? DisplayName { get; init; }
    public List<string> RedirectUris { get; init; } = [];
    public List<string> PostLogoutRedirectUris { get; init; } = [];
    public List<string> Permissions { get; init; } = [];

    public OpenIddictApplicationDescriptor ToDescriptor()
    {
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = ClientId,
            ClientSecret = ClientSecret,
            ClientType = ClientType,
            ConsentType = ConsentType,
            DisplayName = DisplayName,
        };
        foreach (var uri in RedirectUris)
            descriptor.RedirectUris.Add(new Uri(uri));
        foreach (var uri in PostLogoutRedirectUris)
            descriptor.PostLogoutRedirectUris.Add(new Uri(uri));
        foreach (var permission in Permissions)
            descriptor.Permissions.Add(permission);
        return descriptor;
    }
}
