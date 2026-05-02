using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brigade.Admin.Data.Configurations;

public class McpServerOptionsConfiguration : IEntityTypeConfiguration<McpServerOptions>
{
    private const string Schema = "agents";
    public void Configure(EntityTypeBuilder<McpServerOptions> builder)
    {
        builder.ToTable("McpServers", schema: Schema);
        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => new McpServerOptionsId(value));
        builder.HasMany(m => m.Headers)
            .WithOne(h => h.McpServer)
            .HasForeignKey(h => h.McpServerId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
