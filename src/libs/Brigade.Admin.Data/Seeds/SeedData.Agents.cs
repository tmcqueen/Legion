using Brigade.Admin.Data.Models.Agents;

namespace Brigade.Admin.Data.Seeds;

internal static partial class SeedData
{
    public static List<AgentOptions> GetDefaultAgents() => new ()
    {
        new ()
        {
            Name = "Default Agent",
        }
    };
}