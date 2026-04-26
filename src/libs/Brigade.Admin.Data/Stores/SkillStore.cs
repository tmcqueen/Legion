using Brigade.Admin.Data.Models.Agents;
using Microsoft.Extensions.Caching.Memory;

namespace Brigade.Admin.Data.Stores;

public class SkillStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<SkillOptions>(db, cache)
{
    public override string AllKey => "Skills:all";
}
