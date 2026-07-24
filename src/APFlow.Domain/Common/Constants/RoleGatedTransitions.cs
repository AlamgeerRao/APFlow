namespace APFlow.Domain.Common.Constants;

/// <summary>
/// The invoice status transitions that require the acting user to hold a specific
/// role (WP-053 task 3). Lives in APFlow.Domain, not APFlow.Infrastructure's
/// <c>WorkflowTransitionSeedData</c>, specifically so both layers can reference the
/// same single definition: Infrastructure needs it for its seed data's own
/// documentation/consistency, and Application (<c>InvoiceService.UpdateAsync</c>)
/// needs it to actually enforce the gate - and Application must not reference
/// Infrastructure (Solution Structure §2).
///
/// <para>
/// This is deliberately NOT a column on <see cref="Entities.WorkflowTransition"/>.
/// WP-050 left that entity without a "required role" field on purpose, and WP-051
/// established that the answer to "who may perform an approval-type action" lives
/// in <see cref="Entities.ApprovalPolicy"/> (tenant-configurable data), not
/// hardcoded per transition. These pairs identify WHICH transitions are gated; the
/// <c>ApprovalPolicy</c> for <see cref="ApprovalDomains.InvoiceApproval"/> supplies
/// WHICH ROLE each requires. See docs/WP-053-Transition-Enforcement-Decisions.md.
/// </para>
///
/// <para>
/// All four currently resolve to the same required role
/// (<see cref="Roles.FinanceManager"/>, per 06_Domain_Reference_Data.md §1's interim
/// Full/Approver mapping) because they all check the same single
/// <c>InvoiceApproval</c> policy. If a future requirement needs different roles for
/// different transitions, that means introducing a second approval domain (and
/// policy row), not adding a role column here.
/// </para>
/// </summary>
public static class RoleGatedTransitions
{
    private static readonly HashSet<(string FromStatusCode, string ToStatusCode)> Gated =
    [
        // GB Skips only (the platform-default template has no CHECKED_READY_TO_APPROVE status).
        (InvoiceStatusCodes.CheckedReadyToApprove, InvoiceStatusCodes.Approved),
        (InvoiceStatusCodes.CheckedReadyToApprove, InvoiceStatusCodes.NeedsQuery),

        // Reopen paths - present in BOTH templates' graphs.
        (InvoiceStatusCodes.Rejected, InvoiceStatusCodes.AwaitingReview),
        (InvoiceStatusCodes.Cancelled, InvoiceStatusCodes.Received),
    ];

    /// <summary>
    /// Whether moving from <paramref name="fromStatusCode"/> to
    /// <paramref name="toStatusCode"/> requires an approval-policy role check.
    /// Returns false for a no-op (unchanged status) and for every transition not
    /// explicitly listed.
    /// </summary>
    public static bool RequiresApprovalRole(string fromStatusCode, string toStatusCode) =>
        Gated.Contains((fromStatusCode, toStatusCode));

    /// <summary>Every role-gated (from, to) pair - exposed for tests and seed-data consistency checks.</summary>
    public static IReadOnlyCollection<(string FromStatusCode, string ToStatusCode)> All => Gated;
}
