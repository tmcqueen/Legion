using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;

namespace Legion.Admin.Data.Seeds;

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
