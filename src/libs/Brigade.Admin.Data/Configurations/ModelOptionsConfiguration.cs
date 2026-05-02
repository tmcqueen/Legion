using Brigade.Admin.Data.Models;
using Brigade.Admin.Data.Models.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brigade.Admin.Data.Configurations;

public class ModelOptionsConfiguration : IEntityTypeConfiguration<ModelOptions>
{
    public void Configure(EntityTypeBuilder<ModelOptions> builder)
    {
        builder.Property(m => m.Id)
            .HasConversion(id => id.Value, value => new ModelOptionsId(value));
    }
}
