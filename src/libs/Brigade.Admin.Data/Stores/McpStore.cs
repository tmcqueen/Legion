using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Brigade.Admin.Data.Stores;

public class McpStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<McpServerOptions>(db, cache, id => (McpServerOptionsId)id)
{
    public override string AllKey => "Mcps:all";

    protected override IQueryable<McpServerOptions> BuildAllQuery() =>
        Db.Mcps.AsNoTracking().Include(m => m.Headers);

    public override async Task<McpServerOptions?> GetAsync(Guid id, CancellationToken ct = default) =>
        await Db.Mcps.AsNoTracking().Include(m => m.Headers)
            .FirstOrDefaultAsync(m => m.Id == (McpServerOptionsId)id, ct);

    public async Task ReplaceHeadersAsync(Guid mcpId, List<McpServerHeaders> headers, CancellationToken ct = default)
    {
        var typedMcpId = (McpServerOptionsId)mcpId;
        var existing = await Db.McpServerHeaders.Where(h => h.McpServerId == typedMcpId).ToListAsync(ct);
        Db.McpServerHeaders.RemoveRange(existing);
        foreach (var h in headers)
        {
            if (h.Id.Value == Guid.Empty)
                h.Id = McpServerHeadersId.New();
            h.McpServerId = typedMcpId;
        }
        Db.McpServerHeaders.AddRange(headers);
        await Db.SaveChangesAsync(ct);
        InvalidateCache();
    }
}
