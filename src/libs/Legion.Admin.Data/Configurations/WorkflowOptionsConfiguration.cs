using Legion.Admin.Data.Models;
using Legion.Admin.Data.Models.Agents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Legion.Admin.Data.Configurations;

public class WorkflowOptionsConfiguration : IEntityTypeConfiguration<WorkflowOptions>
{
    public void Configure(EntityTypeBuilder<WorkflowOptions> builder)
    {
        builder.Property(w => w.Id)
            .HasConversion(id => id.Value, value => new WorkflowOptionsId(value));
    }
}
