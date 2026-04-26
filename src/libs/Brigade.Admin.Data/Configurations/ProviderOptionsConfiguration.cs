using Brigade.Admin.Data.Models.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brigade.Admin.Data.Configurations;

public class ProviderOptionsConfiguration : IEntityTypeConfiguration<ProviderOptions>
{
    public void Configure(EntityTypeBuilder<ProviderOptions> builder)
    {
        builder.HasMany(p => p.Models)
            .WithMany(m => m.Providers)
            .UsingEntity(t => t.ToTable("ProviderModels"));
    }
}
