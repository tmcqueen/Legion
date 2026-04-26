using Brigade.Admin.Data.Models.Agents;
using Microsoft.Extensions.Caching.Memory;

namespace Brigade.Admin.Data.Stores;

public class WorkflowStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<WorkflowOptions>(db, cache)
{
    public override string AllKey => "Workflows:all";
}
