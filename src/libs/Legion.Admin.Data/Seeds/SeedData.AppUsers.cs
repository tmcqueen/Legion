using Legion.Admin.Data.Auth;
using Legion.Admin.Data.Models.Auth;

namespace Legion.Admin.Data.Seeds;

internal static partial class SeedData
{
    public static List<SeedUser> GetDefaultAppUsers() => new ()
    {
        new SeedUser
        {
            UserName = "admin",
            Email = "admin@legion.local",
            EmailConfirmed = true,
            Password = "Admin123!"
            // Password should be set in production environment
        }
    };

    public class SeedUser : ApplicationUser
    {
        public string Password { get; set; } = null!;
        public ApplicationUser ToApplicationUser() => new ApplicationUser
        {
            UserName = UserName,
            Email = Email,
            EmailConfirmed = EmailConfirmed
        };
    }

}