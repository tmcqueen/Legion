namespace Brigade.Admin.Data.Stores;

public interface IStore<TEntity> where TEntity : class
{
    string AllKey { get; }
    Task<List<TEntity>> GetAllAsync(CancellationToken ct = default);
    Task<TEntity?> GetAsync(Guid id, CancellationToken ct = default);
    Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default);
    Task UpdateAsync(TEntity entity, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
