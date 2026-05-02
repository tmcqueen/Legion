using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;

namespace Brigade.Admin.Data.Stores;

public class MemoryStore(AppDbContext db)
{
    public async Task<List<MemoryOptions>> GetAllAsync(CancellationToken ct = default) =>
        await db.Memories.AsNoTracking().ToListAsync(ct);

    public async Task<MemoryOptions?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var typedId = (MemoryOptionsId)id;
        return await db.Memories.AsNoTracking().FirstOrDefaultAsync(m => m.Id == typedId, ct);
    }

    public async Task<MemoryOptions> AddAsync(MemoryOptions memory, CancellationToken ct = default)
    {
        db.Memories.Add(memory);
        await db.SaveChangesAsync(ct);
        return memory;
    }

    public async Task UpdateAsync(MemoryOptions memory, CancellationToken ct = default)
    {
        db.ChangeTracker.Clear();
        db.Memories.Update(memory);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var typedId = (MemoryOptionsId)id;
        var memory = await db.Memories.FindAsync([typedId], ct);
        if (memory is not null)
        {
            db.Memories.Remove(memory);
            await db.SaveChangesAsync(ct);
        }
    }
}
