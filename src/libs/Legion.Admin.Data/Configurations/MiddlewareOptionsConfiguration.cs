using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class MiddlewareOptionsConfiguration : IEntityTypeConfiguration<MiddlewareOptions>
{
    public void Configure(EntityTypeBuilder<MiddlewareOptions> builder)
    {
        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => new MiddlewareOptionsId(value));
    }
}
