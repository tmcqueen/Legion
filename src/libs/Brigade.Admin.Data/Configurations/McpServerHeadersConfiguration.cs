using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brigade.Admin.Data.Configurations;

public class McpServerHeadersConfiguration : IEntityTypeConfiguration<McpServerHeaders>
{
    public void Configure(EntityTypeBuilder<McpServerHeaders> builder)
    {
        builder.Property(h => h.Id)
            .HasConversion(id => id.Value, value => new McpServerHeadersId(value));
        builder.Property(h => h.McpServerId)
            .HasConversion(id => id.Value, value => new McpServerOptionsId(value));
    }
}
