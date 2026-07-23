using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APFlow.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping and seed data for <see cref="ApprovalPolicy"/> (WP-051 tasks 1/2/3).</summary>
public sealed class ApprovalPolicyConfiguration : IEntityTypeConfiguration<ApprovalPolicy>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<ApprovalPolicy> builder)
    {
        builder.ToTable("ApprovalPolicies");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.Domain).IsRequired().HasMaxLength(100);
        builder.Property(p => p.RequiredRole).IsRequired().HasMaxLength(100);

        builder.HasIndex(p => new { p.Domain, p.TenantId }).IsUnique();

        // Task 3: GB Skips - Invoice Approval domain, FINANCE_MANAGER required,
        // dual control disabled (single approver sufficient per the Requirements
        // Addendum - task 2's confirmed mapping, see
        // docs/WP-051-Approval-Policy-Decisions.md). No platform-default policy
        // seeded for this domain - the platform-default template has no
        // CHECKED_READY_TO_APPROVE status at all (WP-050), so there is nothing for
        // a platform-default InvoiceApproval policy to govern yet. No
        // PaymentBatchApproval policy seeded either - task 5's hook is documented,
        // not built (WP-038 doesn't exist).
        builder.HasData(new
        {
            Id = ApprovalPolicySeedData.GbSkipsInvoiceApprovalPolicyId,
            TenantId = (Guid?)WorkflowSeedData.GbSkipsPlaceholderTenantId,
            Domain = ApprovalDomains.InvoiceApproval,
            RequiredRole = Roles.FinanceManager,
            RequiresDualControl = false,
            CreatedAtUtc = WorkflowSeedData.SeedTimestamp,
            CreatedBy = "system",
            IsDeleted = false,
        });
    }
}
