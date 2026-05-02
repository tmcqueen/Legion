using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Models.Agents;

namespace Brigade.Admin.Data.Seeds;

internal static partial class SeedData
{
    public static List<AgentOptions> GetDefaultAgents() => new ()
    {
        new ()
        {
            Id = AgentOptionsId.New(),
            Name = "Default Agent",
        }
    };
}
