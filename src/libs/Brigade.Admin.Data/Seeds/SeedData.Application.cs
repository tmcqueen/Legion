using OpenIddict.Abstractions;

namespace Brigade.Admin.Data.Seeds;

internal static partial class SeedData
{
    public static List<OpenIddictApplicationDescriptor> GetDefaultApplications(string authority) 
        => new ()
    {
        new OpenIddictApplicationDescriptor
        {
            ClientId = "brigade-bff-client-id",
            ClientSecret = "brigade-bff-client-secret",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            DisplayName = "Brigade BFF",
            RedirectUris = { new Uri($"{authority}/signin-oidc") },
            PostLogoutRedirectUris = { new Uri($"{authority}/signout-callback-oidc") },
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Authorization,
                OpenIddictConstants.Permissions.Endpoints.EndSession,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.AuthorizationCode,
                OpenIddictConstants.Permissions.ResponseTypes.Code,
                "scp:openid",
                OpenIddictConstants.Permissions.Scopes.Profile,
                OpenIddictConstants.Permissions.Prefixes.Scope + "brigade-api",
            }
        },
        new OpenIddictApplicationDescriptor
        {
            ClientId = "brigade-api-client-id",
            ClientSecret = "brigade-api-client-secret",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            DisplayName = "Brigade API Test Client",
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Prefixes.Scope + "brigade-api",
            }
        }
    };
}