using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="WorkflowTransition"/>. Deliberately no
/// <c>HasData</c> seed call - see that entity's doc comment and
/// docs/WP-050-Workflow-Engine-Decisions.md for why no transition rows are seeded
/// by WP-050.
/// </summary>
public sealed class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("WorkflowTransitions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.FromStatusCode).IsRequired().HasMaxLength(64);
        builder.Property(t => t.ToStatusCode).IsRequired().HasMaxLength(64);

        builder.HasIndex(t => new { t.WorkflowTemplateId, t.FromStatusCode, t.ToStatusCode }).IsUnique();
    }
}
