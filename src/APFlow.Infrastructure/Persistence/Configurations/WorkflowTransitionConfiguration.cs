using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="WorkflowTransition"/>. Seeds exactly ONE row -
/// see below - all other transitions proposed in
/// docs/WP-050-Workflow-Engine-Decisions.md remain unconfirmed and unseeded.
/// </summary>
public sealed class WorkflowTransitionConfiguration : IEntityTypeConfiguration<WorkflowTransition>
{
    /// <summary>Fixed id for the one seeded transition row - see <see cref="Configure"/>.</summary>
    public static readonly Guid GbSkipsCheckedReadyToApproveToApprovedTransitionId = Guid.Parse("00000000-0000-0000-0005-000000000001");

    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WorkflowTransition> builder)
    {
        builder.ToTable("WorkflowTransitions");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.FromStatusCode).IsRequired().HasMaxLength(64);
        builder.Property(t => t.ToStatusCode).IsRequired().HasMaxLength(64);

        builder.HasIndex(t => new { t.WorkflowTemplateId, t.FromStatusCode, t.ToStatusCode }).IsUnique();

        // WP-051 task 4 explicitly directs gating THIS transition by role, which
        // this codebase reads as confirming this one edge specifically - not the
        // rest of WP-050's proposed set (AWAITING_REVIEW -> CHECKED_READY_TO_APPROVE,
        // the NEEDS_REVIEW_FEBINA escalation/resolution edges), which remain
        // unconfirmed and unseeded. See docs/WP-051-Approval-Policy-Decisions.md.
        // Note this row alone is not, by itself, "enforced": InvoiceService.UpdateAsync
        // does not call IWorkflowValidationService for general transition checking
        // (still blocked on the platform-default graph being undocumented - see
        // WP-050) - WP-051 instead adds a narrow, separate role-gate specifically
        // for this transition (see InvoiceService.UpdateAsync's own comments).
        builder.HasData(new
        {
            Id = GbSkipsCheckedReadyToApproveToApprovedTransitionId,
            TenantId = (Guid?)WorkflowSeedData.GbSkipsPlaceholderTenantId,
            WorkflowTemplateId = WorkflowSeedData.GbSkipsTemplateId,
            FromStatusCode = InvoiceStatusCodes.CheckedReadyToApprove,
            ToStatusCode = InvoiceStatusCodes.Approved,
            CreatedAtUtc = WorkflowSeedData.SeedTimestamp,
            CreatedBy = "system",
            IsDeleted = false,
        });
    }
}
