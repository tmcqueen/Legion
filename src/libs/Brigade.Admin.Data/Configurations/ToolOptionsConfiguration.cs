using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brigade.Admin.Data.Configurations;

public class ToolOptionsConfiguration : IEntityTypeConfiguration<ToolOptions>
{
    public void Configure(EntityTypeBuilder<ToolOptions> builder)
    {
        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => new ToolOptionsId(value));
    }
}
