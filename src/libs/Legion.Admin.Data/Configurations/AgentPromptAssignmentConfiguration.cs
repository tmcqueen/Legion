using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Prompts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class AgentPromptAssignmentConfiguration : IEntityTypeConfiguration<AgentPromptAssignment>
{
    public void Configure(EntityTypeBuilder<AgentPromptAssignment> builder)
    {
        builder.Property(a => a.Id)
            .HasConversion(id => id.Value, value => new AgentPromptAssignmentId(value));

        builder.Property(a => a.AgentId)
            .HasConversion(id => id.Value, value => new AgentOptionsId(value));

        builder.Property(a => a.DefinitionId)
            .HasConversion(id => id.Value, value => new PromptDefinitionId(value));

        builder.Property(a => a.Order)
            .HasDefaultValue(0);

        builder.HasIndex(a => new { a.AgentId, a.DefinitionId })
            .IsUnique()
            .HasDatabaseName("ix_agent_prompt_assignments_agent_definition");
    }
}
