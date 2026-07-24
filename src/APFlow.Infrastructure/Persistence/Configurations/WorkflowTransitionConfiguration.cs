using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping and seed data for <see cref="WorkflowTransition"/>. WP-053 seeds
/// both templates' full confirmed transition graphs here (see
/// <see cref="WorkflowTransitionSeedData"/>), replacing WP-051's single
/// provisionally-confirmed row - this is what finally closes WP-050's central open
/// item.
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

        builder.HasData(WorkflowTransitionSeedData.All.Select(row => new
        {
            row.Id,
            TenantId = row.TemplateId == WorkflowSeedData.GbSkipsTemplateId
                ? (Guid?)WorkflowSeedData.GbSkipsPlaceholderTenantId
                : null,
            WorkflowTemplateId = row.TemplateId,
            row.FromStatusCode,
            row.ToStatusCode,
            CreatedAtUtc = WorkflowSeedData.SeedTimestamp,
            CreatedBy = "system",
            IsDeleted = false,
        }));
    }
}
