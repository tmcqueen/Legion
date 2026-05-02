using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Prompts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class PromptVersionConfiguration : IEntityTypeConfiguration<PromptVersion>
{
    public void Configure(EntityTypeBuilder<PromptVersion> builder)
    {
        builder.Property(v => v.Id)
            .HasConversion(id => id.Value, value => new PromptVersionId(value));

        builder.Property(v => v.DefinitionId)
            .HasConversion(id => id.Value, value => new PromptDefinitionId(value));

        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(v => v.Content)
            .IsRequired();

        builder.Property(v => v.CreatedBy)
            .HasMaxLength(512)
            .IsRequired();

        // Filtered unique index: only one Published version per definition
        builder.HasIndex(v => v.DefinitionId)
            .HasFilter("\"Status\" = 'Published'")
            .IsUnique()
            .HasDatabaseName("ix_prompt_versions_definition_published");
    }
}
