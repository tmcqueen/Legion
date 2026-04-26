using Brigade.Admin.Data.Models.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Brigade.Admin.Data.Stores;

public class ProviderStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<ProviderOptions>(db, cache)
{
    public override string AllKey => "Providers:all";

    protected override IQueryable<ProviderOptions> BuildAllQuery() =>
        Db.Providers.AsNoTracking().Include(p => p.Models);

    public override async Task<ProviderOptions?> GetAsync(int id, CancellationToken ct = default) =>
        await Db.Providers.AsNoTracking().Include(p => p.Models)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task AssignModelsAsync(int providerId, IEnumerable<int> modelIds, CancellationToken ct = default)
    {
        var provider = await Db.Providers.Include(p => p.Models).FirstOrDefaultAsync(p => p.Id == providerId, ct);
        if (provider is null) return;
        var idList = modelIds.ToList();
        var models = await Db.Models.Where(m => idList.Contains(m.Id)).ToListAsync(ct);
        provider.Models.Clear();
        provider.Models.AddRange(models);
        await Db.SaveChangesAsync(ct);
        InvalidateCache();
    }
}
