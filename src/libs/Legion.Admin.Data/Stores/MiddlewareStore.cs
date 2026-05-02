using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.Extensions.Caching.Memory;

namespace Legion.Admin.Data.Stores;

public class MiddlewareStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<MiddlewareOptions>(db, cache, id => (MiddlewareOptionsId)id)
{
    public override string AllKey => "Middleware:all";
}
