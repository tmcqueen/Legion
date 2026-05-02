using OpenIddict.Abstractions;

namespace Legion.Admin.Data.Seeds;

internal static partial class SeedData
{
    public static List<OpenIddictApplicationDescriptor> GetDefaultApplications(string authority) 
        => new ()
    {
        new OpenIddictApplicationDescriptor
        {
            ClientId = "legion-bff-client-id",
            ClientSecret = "legion-bff-client-secret",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            ConsentType = OpenIddictConstants.ConsentTypes.Implicit,
            DisplayName = "Legion BFF",
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
                OpenIddictConstants.Permissions.Prefixes.Scope + "legion-api",
            }
        },
        new OpenIddictApplicationDescriptor
        {
            ClientId = "legion-api-client-id",
            ClientSecret = "legion-api-client-secret",
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            DisplayName = "Legion API Test Client",
            Permissions =
            {
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Prefixes.Scope + "legion-api",
            }
        }
    };
}