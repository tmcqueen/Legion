using Brigade.Admin.Data.Models.Agents;
using Microsoft.Extensions.Caching.Memory;

namespace Brigade.Admin.Data.Stores;

public class ToolStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<ToolOptions>(db, cache)
{
    public override string AllKey => "Tools:all";
}
