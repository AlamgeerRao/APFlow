namespace APFlow.Infrastructure.Persistence;

/// <summary>
/// Fixed, deterministic data for WP-051's seeded GB Skips <c>ApprovalPolicy</c> row,
/// used by <c>ApprovalPolicyConfiguration</c>'s <c>HasData</c> call. See
/// <see cref="WorkflowSeedData"/> for the equivalent WP-050 data and the reasoning
/// behind using fixed ids for seed data in general.
/// </summary>
public static class ApprovalPolicySeedData
{
    /// <summary>Fixed id for GB Skips' InvoiceApproval policy seed row.</summary>
    public static readonly Guid GbSkipsInvoiceApprovalPolicyId = Guid.Parse("00000000-0000-0000-0004-000000000001");
}
