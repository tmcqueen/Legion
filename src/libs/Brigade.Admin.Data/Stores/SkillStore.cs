using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Models.Agents;
using Microsoft.Extensions.Caching.Memory;

namespace Brigade.Admin.Data.Stores;

public class SkillStore(AppDbContext db, IMemoryCache cache)
    : CatalogStore<SkillOptions>(db, cache, id => (SkillOptionsId)id)
{
    public override string AllKey => "Skills:all";
}
