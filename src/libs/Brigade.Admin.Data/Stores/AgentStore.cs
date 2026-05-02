using Brigade.Admin.Data.Models;
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

    public async Task<AgentOptions?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var typedId = (AgentOptionsId)id;
        return await db.Agents.AsNoTracking()
            .Include(a => a.Provider)
            .Include(a => a.Tools)
            .Include(a => a.Models)
            .Include(a => a.Skills)
            .Include(a => a.McpServers)
            .Include(a => a.Middleware)
            .Include(a => a.Memory)
            .FirstOrDefaultAsync(a => a.Id == typedId, ct);
    }

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

    public async Task AssignToolsAsync(Guid agentId, IEnumerable<Guid> toolIds, CancellationToken ct = default)
    {
        var typedAgentId = (AgentOptionsId)agentId;
        var agent = await db.Agents.Include(a => a.Tools).FirstOrDefaultAsync(a => a.Id == typedAgentId, ct);
        if (agent is null) return;
        var typedToolIds = toolIds.Select(g => (ToolOptionsId)g).ToList();
        var tools = await db.Tools.Where(t => typedToolIds.Contains(t.Id)).ToListAsync(ct);
        agent.Tools.Clear();
        agent.Tools.AddRange(tools);
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignModelsAsync(Guid agentId, IEnumerable<Guid> modelIds, CancellationToken ct = default)
    {
        var typedAgentId = (AgentOptionsId)agentId;
        var agent = await db.Agents.Include(a => a.Models).FirstOrDefaultAsync(a => a.Id == typedAgentId, ct);
        if (agent is null) return;
        var typedModelIds = modelIds.Select(g => (ModelOptionsId)g).ToList();
        var models = await db.Models.Where(m => typedModelIds.Contains(m.Id)).ToListAsync(ct);
        agent.Models.Clear();
        agent.Models.AddRange(models);
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignSkillsAsync(Guid agentId, IEnumerable<Guid> skillIds, CancellationToken ct = default)
    {
        var typedAgentId = (AgentOptionsId)agentId;
        var agent = await db.Agents.Include(a => a.Skills).FirstOrDefaultAsync(a => a.Id == typedAgentId, ct);
        if (agent is null) return;
        var typedSkillIds = skillIds.Select(g => (SkillOptionsId)g).ToList();
        var skills = await db.Skills.Where(s => typedSkillIds.Contains(s.Id)).ToListAsync(ct);
        agent.Skills.Clear();
        agent.Skills.AddRange(skills);
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignMcpServersAsync(Guid agentId, IEnumerable<Guid> mcpIds, CancellationToken ct = default)
    {
        var typedAgentId = (AgentOptionsId)agentId;
        var agent = await db.Agents.Include(a => a.McpServers).FirstOrDefaultAsync(a => a.Id == typedAgentId, ct);
        if (agent is null) return;
        var typedMcpIds = mcpIds.Select(g => (McpServerOptionsId)g).ToList();
        var mcps = await db.Mcps.Where(m => typedMcpIds.Contains(m.Id)).ToListAsync(ct);
        agent.McpServers.Clear();
        agent.McpServers.AddRange(mcps);
        await db.SaveChangesAsync(ct);
    }

    public async Task AssignMiddlewareAsync(Guid agentId, IEnumerable<Guid> middlewareIds, CancellationToken ct = default)
    {
        var typedAgentId = (AgentOptionsId)agentId;
        var agent = await db.Agents.Include(a => a.Middleware).FirstOrDefaultAsync(a => a.Id == typedAgentId, ct);
        if (agent is null) return;
        var typedMiddlewareIds = middlewareIds.Select(g => (MiddlewareOptionsId)g).ToList();
        var middleware = await db.Middlewares.Where(m => typedMiddlewareIds.Contains(m.Id)).ToListAsync(ct);
        agent.Middleware.Clear();
        agent.Middleware.AddRange(middleware);
        await db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var typedId = (AgentOptionsId)id;
        var agent = await db.Agents.FindAsync([typedId], ct);
        if (agent is not null)
        {
            db.Agents.Remove(agent);
            await db.SaveChangesAsync(ct);
        }
    }
}
