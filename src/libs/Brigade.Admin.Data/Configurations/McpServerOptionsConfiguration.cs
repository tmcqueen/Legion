using Brigade.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brigade.Admin.Data.Configurations;

public class McpServerOptionsConfiguration : IEntityTypeConfiguration<McpServerOptions>
{
    public void Configure(EntityTypeBuilder<McpServerOptions> builder)
    {
        builder.HasMany(m => m.Headers)
            .WithOne(h => h.McpServer)
            .HasForeignKey(h => h.McpServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
