using OpenIddict.Abstractions;

namespace Brigade.Admin.Data.Seeds;

internal static partial class SeedData
{
    public static List<OpenIddictScopeDescriptor> GetDefaultAppScopes() => new ()
    {
        new OpenIddictScopeDescriptor
        {
            Name = "brigade-api",
            Resources = { "brigade-webhost" }
        }
    };
}