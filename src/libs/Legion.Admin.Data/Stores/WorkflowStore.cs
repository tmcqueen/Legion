using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.Extensions.Caching.Memory;

namespace Legion.Admin.Data.Stores;

public class WorkflowStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<WorkflowOptions>(db, cache, id => (WorkflowOptionsId)id)
{
    public override string AllKey => "Workflows:all";
}
