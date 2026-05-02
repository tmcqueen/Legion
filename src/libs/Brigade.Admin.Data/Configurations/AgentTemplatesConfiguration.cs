using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
namespace Brigade.Admin.Data.Configurations;

public class AgentTemplatesConfiguration : IEntityTypeConfiguration<AgentTemplate>
{
    public void Configure(EntityTypeBuilder<AgentTemplate> builder)
    {
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new AgentOptionsId(value));
        builder.Property(a => a.ProviderId)
            .HasConversion(id => id.Value, value => new ProviderOptionsId(value));

        builder.HasMany(a => a.McpServers);

        builder.HasOne(a => a.Memory);

        builder.HasMany(a => a.Middleware);

        builder.HasMany(a => a.Models);

        builder.HasOne(a => a.Provider);

        builder.HasMany(a => a.Tools);

        builder.HasMany(a => a.Skills);
    }
}
