using Brigade.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Brigade.Admin.Data.Stores;

public class McpStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<McpServerOptions>(db, cache)
{
    public override string AllKey => "Mcps:all";

    protected override IQueryable<McpServerOptions> BuildAllQuery() =>
        Db.Mcps.AsNoTracking().Include(m => m.Headers);

    public override async Task<McpServerOptions?> GetAsync(int id, CancellationToken ct = default) =>
        await Db.Mcps.AsNoTracking().Include(m => m.Headers)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

    public async Task ReplaceHeadersAsync(int mcpId, List<McpServerHeaders> headers, CancellationToken ct = default)
    {
        var existing = await Db.McpServerHeaders.Where(h => h.McpServerId == mcpId).ToListAsync(ct);
        Db.McpServerHeaders.RemoveRange(existing);
        foreach (var h in headers)
            h.McpServerId = mcpId;
        Db.McpServerHeaders.AddRange(headers);
        await Db.SaveChangesAsync(ct);
        InvalidateCache();
    }
}
