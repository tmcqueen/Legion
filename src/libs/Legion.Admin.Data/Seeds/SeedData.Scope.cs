using OpenIddict.Abstractions;

namespace Legion.Admin.Data.Seeds;

internal static partial class SeedData
{
    public static List<OpenIddictScopeDescriptor> GetDefaultAppScopes() => new ()
    {
        new OpenIddictScopeDescriptor
        {
            Name = "legion-api",
            Resources = { "legion-webhost" }
        }
    };
}