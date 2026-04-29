using Brigade.Admin.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brigade.Admin.Data.Configurations;

public class SecretOptionsConfiguration : IEntityTypeConfiguration<SecretOptions>
{
    public void Configure(EntityTypeBuilder<SecretOptions> builder)
    {
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Path).IsRequired().HasMaxLength(500);
        builder.Property(s => s.EncryptedValue).IsRequired();
        builder.HasIndex(s => s.Path).IsUnique();
    }
}
