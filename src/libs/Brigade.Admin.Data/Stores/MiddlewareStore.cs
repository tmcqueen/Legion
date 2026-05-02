using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Models.Agents;
using Microsoft.Extensions.Caching.Memory;

namespace Brigade.Admin.Data.Stores;

public class MiddlewareStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<MiddlewareOptions>(db, cache, id => (MiddlewareOptionsId)id)
{
    public override string AllKey => "Middleware:all";
}
