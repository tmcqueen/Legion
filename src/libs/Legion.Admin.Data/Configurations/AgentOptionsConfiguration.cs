using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class AgentOptionsConfiguration : IEntityTypeConfiguration<AgentOptions>
{
    private const string Schema = "agents";

    public void Configure(EntityTypeBuilder<AgentOptions> builder)
    {
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new AgentOptionsId(value));
        builder.Property(a => a.ProviderId)
            .HasConversion(id => id.Value, value => new ProviderOptionsId(value));

        builder.Ignore(a => a.ProviderName);

        builder.HasMany(a => a.McpServers)
            .WithMany(m => m.Agents)
            .UsingEntity(t => t.ToTable("AgentMcpServers", schema: Schema));

        builder.HasOne(a => a.Memory)
            .WithOne(m => m.Agent)
            .HasForeignKey<MemoryOptions>(m => m.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(a => a.Middleware)
            .WithMany(m => m.Agents)
            .UsingEntity(t => t.ToTable("AgentMiddleware", schema: Schema));

        builder.HasMany(a => a.Models)
            .WithMany(m => m.Agents)
            .UsingEntity(t => t.ToTable("AgentModels", schema: Schema));

        builder.HasOne(a => a.Provider)
            .WithMany(p => p.Agents)
            .HasForeignKey(a => a.ProviderId);

        builder.HasMany(a => a.Tools)
            .WithMany(t => t.Agents)
            .UsingEntity(t => t.ToTable("AgentTools", schema: Schema));

        builder.HasMany(a => a.Skills)
            .WithMany(s => s.Agents)
            .UsingEntity(t => t.ToTable("AgentSkills", schema: Schema));
    }
}
