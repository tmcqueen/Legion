using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Providers;
using Microsoft.Extensions.Caching.Memory;

namespace Legion.Admin.Data.Stores;

public class ModelStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<ModelOptions>(db, cache, id => (ModelOptionsId)id)
{
    public override string AllKey => "Models:all";
}
