using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Legion.Admin.Data.Stores;

public class ProviderStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<ProviderOptions>(db, cache, id => (ProviderOptionsId)id)
{
    public override string AllKey => "Providers:all";

    protected override IQueryable<ProviderOptions> BuildAllQuery() =>
        Db.Providers.AsNoTracking()
            .Include(p => p.Models)
            .Include(p => p.ApiTokenSecret);

    public override async Task<ProviderOptions?> GetAsync(Guid id, CancellationToken ct = default) =>
        await Db.Providers.AsNoTracking()
            .Include(p => p.Models)
            .Include(p => p.ApiTokenSecret)
            .FirstOrDefaultAsync(p => p.Id == (ProviderOptionsId)id, ct);

    public async Task AssignModelsAsync(Guid providerId, IEnumerable<Guid> modelIds, CancellationToken ct = default)
    {
        var typedProviderId = (ProviderOptionsId)providerId;
        var provider = await Db.Providers.Include(p => p.Models)
            .FirstOrDefaultAsync(p => p.Id == typedProviderId, ct);
        if (provider is null) return;
        var typedIds = modelIds.Select(g => (ModelOptionsId)g).ToList();
        var models = await Db.Models.Where(m => typedIds.Contains(m.Id)).ToListAsync(ct);
        provider.Models.Clear();
        provider.Models.AddRange(models);
        await Db.SaveChangesAsync(ct);
        InvalidateCache();
    }
}
