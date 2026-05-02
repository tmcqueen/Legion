using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.Extensions.Caching.Memory;

namespace Legion.Admin.Data.Stores;

public class ToolStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<ToolOptions>(db, cache, id => (ToolOptionsId)id)
{
    public override string AllKey => "Tools:all";
}
