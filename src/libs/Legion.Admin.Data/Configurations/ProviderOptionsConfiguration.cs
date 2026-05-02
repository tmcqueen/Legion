using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class ProviderOptionsConfiguration : IEntityTypeConfiguration<ProviderOptions>
{
    private const string Schema = "agents";

    public void Configure(EntityTypeBuilder<ProviderOptions> builder)
    {
        builder.ToTable("Providers", schema: Schema);
        builder.Property(p => p.Id)
            .HasConversion(id => id.Value, value => new ProviderOptionsId(value));
        builder.HasMany(p => p.Models)
            .WithMany(m => m.Providers)
            .UsingEntity(t => t.ToTable("ProviderModels", schema: Schema));
    }
}
