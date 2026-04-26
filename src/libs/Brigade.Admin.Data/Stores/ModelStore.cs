using Brigade.Admin.Data.Models.Providers;
using Microsoft.Extensions.Caching.Memory;

namespace Brigade.Admin.Data.Stores;

public class ModelStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<ModelOptions>(db, cache)
{
    public override string AllKey => "Models:all";
}
