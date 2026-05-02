using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class ToolOptionsConfiguration : IEntityTypeConfiguration<ToolOptions>
{
    public void Configure(EntityTypeBuilder<ToolOptions> builder)
    {
        builder.Property(t => t.Id)
            .HasConversion(id => id.Value, value => new ToolOptionsId(value));
    }
}
