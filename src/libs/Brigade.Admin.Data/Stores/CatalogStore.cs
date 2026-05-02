using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Brigade.Admin.Data.Stores;

public abstract class CatalogStore<TEntity>(
    AppDbContext db,
    IMemoryCache cache,
    Func<Guid, object> keyFactory)
    : IStore<TEntity>
    where TEntity : class
{
    protected AppDbContext Db { get; } = db;
    public abstract string AllKey { get; }

    private static readonly MemoryCacheEntryOptions CacheOptions =
        new() { SlidingExpiration = TimeSpan.FromMinutes(5) };

    protected void InvalidateCache() => cache.Remove(AllKey);

    public async Task<List<TEntity>> GetAllAsync(CancellationToken ct = default) =>
        (await cache.GetOrCreateAsync(AllKey, async e =>
        {
            e.SetOptions(CacheOptions);
            return await BuildAllQuery().ToListAsync(ct);
        }))!;

    protected virtual IQueryable<TEntity> BuildAllQuery() =>
        Db.Set<TEntity>().AsNoTracking();

    public virtual async Task<TEntity?> GetAsync(Guid id, CancellationToken ct = default) =>
        await Db.Set<TEntity>().FindAsync([keyFactory(id)], ct);

    public async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
    {
        Db.Set<TEntity>().Add(entity);
        await Db.SaveChangesAsync(ct);
        cache.Remove(AllKey);
        return entity;
    }

    public async Task UpdateAsync(TEntity entity, CancellationToken ct = default)
    {
        Db.ChangeTracker.Clear();
        Db.Set<TEntity>().Update(entity);
        await Db.SaveChangesAsync(ct);
        cache.Remove(AllKey);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await Db.Set<TEntity>().FindAsync([keyFactory(id)], ct);
        if (entity is not null)
        {
            Db.Set<TEntity>().Remove(entity);
            await Db.SaveChangesAsync(ct);
            cache.Remove(AllKey);
        }
    }
}
