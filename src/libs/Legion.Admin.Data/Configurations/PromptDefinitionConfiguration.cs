using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Prompts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class PromptDefinitionConfiguration : IEntityTypeConfiguration<PromptDefinition>
{
    public void Configure(EntityTypeBuilder<PromptDefinition> builder)
    {
        builder.Property(d => d.Id)
            .HasConversion(id => id.Value, value => new PromptDefinitionId(value));

        builder.Property(d => d.Path)
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(d => d.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.Category)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(d => d.IsDefaultIncluded)
            .HasDefaultValue(false);

        builder.Property(d => d.CreatedBy)
            .HasMaxLength(512)
            .IsRequired();

        builder.HasIndex(d => d.Path)
            .IsUnique()
            .HasDatabaseName("ix_prompt_definitions_path");

        builder.HasMany(d => d.Versions)
            .WithOne(v => v.Definition)
            .HasForeignKey(v => v.DefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(d => d.Assignments)
            .WithOne(a => a.Definition)
            .HasForeignKey(a => a.DefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
