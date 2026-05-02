using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class SkillOptionsConfiguration : IEntityTypeConfiguration<SkillOptions>
{
    public void Configure(EntityTypeBuilder<SkillOptions> builder)
    {
        builder.Property(s => s.Id)
            .HasConversion(id => id.Value, value => new SkillOptionsId(value));
    }
}
