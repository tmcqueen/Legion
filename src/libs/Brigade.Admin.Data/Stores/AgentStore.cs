using Brigade.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;

namespace Brigade.Admin.Data.Stores;

public class AgentStore(AppDbContext db)
{
    public async Task<List<AgentOptions>> GetAllAsync(CancellationToken ct = default) =>
        await db.Agents.AsNoTracking()
            .Include(a => a.Provider)
            .Include(a => a.Tools)
            .Include(a => a.Models)
            .Include(a => a.Skills)
            .Include(a => a.McpServers)
            .Include(a => a.Middleware)
            .ToListAsync(ct);

    public async Task<AgentOptions?> GetAsync(int id, CancellationToken ct = default) =>
        await db.Agents.AsNoTracking()
            .Include(a => a.Provider)
            .Include(a => a.Tools)
            .Include(a => a.Models)
            .Include(a => a.Skills)
            .Include(a => a.McpServers)
            .Include(a => a.Middleware)
            .Include(a => a.Memory)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

    public async Task<AgentOptions> AddAsync(AgentOptions agent, CancellationToken ct = default)
    {
        db.Agents.Add(agent);
        await db.SaveChangesAsync(ct);
        return agent;
    }

    public async Task UpdateAsync(AgentOptions agent, CancellationToken ct = default)
    {
        db.ChangeTracker.Clear();
        db.Agents.Update(agent);
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignToolsAsync(int agentId, IEnumerable<int> toolIds, CancellationToken ct = default)
    {
        var agent = await db.Agents.Include(a => a.Tools).FirstOrDefaultAsync(a => a.Id == agentId, ct);
        if (agent is null) return;
        var tools = await db.Tools.Where(t => toolIds.Contains(t.Id)).ToListAsync(ct);
        agent.Tools.Clear();
        agent.Tools.AddRange(tools);
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignModelsAsync(int agentId, IEnumerable<int> modelIds, CancellationToken ct = default)
    {
        var agent = await db.Agents.Include(a => a.Models).FirstOrDefaultAsync(a => a.Id == agentId, ct);
        if (agent is null) return;
        var models = await db.Models.Where(m => modelIds.Contains(m.Id)).ToListAsync(ct);
        agent.Models.Clear();
        agent.Models.AddRange(models);
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignSkillsAsync(int agentId, IEnumerable<int> skillIds, CancellationToken ct = default)
    {
        var agent = await db.Agents.Include(a => a.Skills).FirstOrDefaultAsync(a => a.Id == agentId, ct);
        if (agent is null) return;
        var skills = await db.Skills.Where(s => skillIds.Contains(s.Id)).ToListAsync(ct);
        agent.Skills.Clear();
        agent.Skills.AddRange(skills);
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignMcpServersAsync(int agentId, IEnumerable<int> mcpIds, CancellationToken ct = default)
    {
        var agent = await db.Agents.Include(a => a.McpServers).FirstOrDefaultAsync(a => a.Id == agentId, ct);
        if (agent is null) return;
        var mcps = await db.Mcps.Where(m => mcpIds.Contains(m.Id)).ToListAsync(ct);
        agent.McpServers.Clear();
        agent.McpServers.AddRange(mcps);
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignMiddlewareAsync(int agentId, IEnumerable<int> middlewareIds, CancellationToken ct = default)
    {
        var agent = await db.Agents.Include(a => a.Middleware).FirstOrDefaultAsync(a => a.Id == agentId, ct);
        if (agent is null) return;
        var middleware = await db.Middlewares.Where(m => middlewareIds.Contains(m.Id)).ToListAsync(ct);
        agent.Middleware.Clear();
        agent.Middleware.AddRange(middleware);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var agent = await db.Agents.FindAsync([id], ct);
        if (agent is not null)
        {
            db.Agents.Remove(agent);
            await db.SaveChangesAsync(ct);
        }
    }
}
