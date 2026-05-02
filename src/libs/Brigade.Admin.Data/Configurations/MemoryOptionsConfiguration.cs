using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brigade.Admin.Data.Configurations;

public class MemoryOptionsConfiguration : IEntityTypeConfiguration<MemoryOptions>
{
    public void Configure(EntityTypeBuilder<MemoryOptions> builder)
    {
        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => new MemoryOptionsId(value));
        builder.Property(m => m.AgentId)
            .HasConversion(id => id.Value, value => new AgentOptionsId(value));
    }
}
