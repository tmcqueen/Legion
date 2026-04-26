using Brigade.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brigade.Admin.Data.Configurations;

public class AgentOptionsConfiguration : IEntityTypeConfiguration<AgentOptions>
{
    public void Configure(EntityTypeBuilder<AgentOptions> builder)
    {
        builder.HasMany(a => a.McpServers)
            .WithMany(m => m.Agents)
            .UsingEntity(t => t.ToTable("AgentMcpServers"));

        builder.HasOne(a => a.Memory)
            .WithOne(m => m.Agent)
            .HasForeignKey<MemoryOptions>(m => m.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Middleware)
            .WithMany(m => m.Agents)
            .UsingEntity(t => t.ToTable("AgentMiddleware"));

        builder.HasMany(a => a.Models)
            .WithMany(m => m.Agents)
            .UsingEntity(t => t.ToTable("AgentModels"));

        builder.HasOne(a => a.Provider)
            .WithMany(p => p.Agents)
            .HasForeignKey(a => a.ProviderId);

        builder.HasMany(a => a.Tools)
            .WithMany(t => t.Agents)
            .UsingEntity(t => t.ToTable("AgentTools"));

        builder.HasMany(a => a.Skills)
            .WithMany(s => s.Agents)
            .UsingEntity(t => t.ToTable("AgentSkills"));
    }
}
