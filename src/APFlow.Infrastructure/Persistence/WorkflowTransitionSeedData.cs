using APFlow.Domain.Common.Constants;

namespace APFlow.Infrastructure.Persistence;

/// <summary>
/// The confirmed transition graphs for both workflow templates (WP-053), seeded via
/// <c>WorkflowTransitionConfiguration</c>'s <c>HasData</c> call. This is the data
/// that finally closes WP-050's central open item - both graphs were previously
/// unseeded because neither had been confirmed (see
/// docs/WP-050-Workflow-Engine-Decisions.md).
///
/// <para>
/// Ids are deterministic, derived from each row's own (template, from, to) triple
/// rather than hand-assigned - see <see cref="BuildId"/>. Hand-assigning ~40 fixed
/// GUIDs would be error-prone and unreviewable; deriving them means the same
/// logical transition always produces the same id, which is exactly what EF Core's
/// <c>HasData</c> requires for stable, reproducible migrations.
/// </para>
///
/// <para>
/// <b>Role gating is NOT stored on these rows.</b> <see cref="Domain.Entities.WorkflowTransition"/>
/// deliberately has no "required role" column (see its own doc comment - WP-050
/// left that to WP-051, which instead put the answer in <c>ApprovalPolicy</c>).
/// WP-053's four role-gated transitions are enforced by
/// <c>InvoiceService.UpdateAsync</c> against
/// <see cref="APFlow.Domain.Common.Constants.RoleGatedTransitions"/>
/// below, checked via the same <c>IApprovalAuthorizationService</c>/<c>ApprovalPolicy</c>
/// mechanism WP-051 established - not by a new column here. See
/// docs/WP-053-Transition-Enforcement-Decisions.md.
/// </para>
///
/// <para>
/// <b>DUPLICATE_SUSPECTED has no transitions.</b> It remains a seeded, valid
/// <c>StatusReference</c> row in both templates (removing it would be a schema/data
/// change nobody has approved - <c>06_Domain_Reference_Data.md</c> §2 still lists
/// it, marked "under review... do not assume it is still reachable until that is
/// resolved"), but WP-053's confirmed graphs contain no edge into or out of it, so
/// it is now genuinely unreachable in practice. See
/// docs/WP-053-Transition-Enforcement-Decisions.md.
/// </para>
/// </summary>
public static class WorkflowTransitionSeedData
{
    /// <summary>One seeded transition edge.</summary>
    /// <param name="TemplateId">The owning template.</param>
    /// <param name="FromStatusCode">The status this transition starts from.</param>
    /// <param name="ToStatusCode">The status this transition ends at.</param>
    public sealed record TransitionSeedRow(Guid TemplateId, string FromStatusCode, string ToStatusCode)
    {
        /// <summary>Deterministic id for this row - see <see cref="BuildId"/>.</summary>
        public Guid Id => BuildId(TemplateId, FromStatusCode, ToStatusCode);
    }

    /// <summary>
    /// Statuses an invoice may be CANCELLED from ("RECEIVED … AWAITING_SUPPLIER_RESPONSE"
    /// in WP-053's table) - i.e. every pre-decision status in lifecycle order, up to
    /// and including AWAITING_SUPPLIER_RESPONSE. Deliberately excludes
    /// DUPLICATE_SUSPECTED (see this class's own doc comment) and every
    /// post-decision status (APPROVED onwards), which have their own explicit rows.
    /// </summary>
    private static readonly string[] CancellableStatuses =
    [
        InvoiceStatusCodes.Received,
        InvoiceStatusCodes.Processing,
        InvoiceStatusCodes.Extracted,
        InvoiceStatusCodes.AwaitingReview,
        InvoiceStatusCodes.NeedsQuery,
        InvoiceStatusCodes.QueryRaised,
        InvoiceStatusCodes.AwaitingSupplierResponse,
    ];

    /// <summary>Statuses a reviewer may REJECT from, per WP-053's table.</summary>
    private static readonly string[] RejectableStatuses =
    [
        InvoiceStatusCodes.AwaitingReview,
        InvoiceStatusCodes.QueryRaised,
        InvoiceStatusCodes.AwaitingSupplierResponse,
    ];

    /// <summary>Terminal-ish statuses that may be ARCHIVED for housekeeping/retention, per WP-053's table.</summary>
    private static readonly string[] ArchivableStatuses =
    [
        InvoiceStatusCodes.Paid,
        InvoiceStatusCodes.Rejected,
        InvoiceStatusCodes.Cancelled,
    ];

    /// <summary>Every seeded transition row, across both templates.</summary>
    public static IReadOnlyList<TransitionSeedRow> All { get; } =
        BuildPlatformDefaultTransitions().Concat(BuildGbSkipsTransitions()).ToList();

    /// <summary>
    /// The platform-default template's confirmed graph (WP-053 task 1). Includes
    /// AWAITING_REVIEW -> APPROVED (direct approval), which GB Skips' graph
    /// deliberately does NOT have.
    /// </summary>
    private static IEnumerable<TransitionSeedRow> BuildPlatformDefaultTransitions()
    {
        var templateId = WorkflowSeedData.PlatformDefaultTemplateId;

        foreach (var row in BuildSharedTransitions(templateId))
        {
            yield return row;
        }

        // Direct reviewer approval - platform-default only. GB Skips routes through
        // CHECKED_READY_TO_APPROVE instead (see BuildGbSkipsTransitions).
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.AwaitingReview, InvoiceStatusCodes.Approved);
    }

    /// <summary>
    /// GB Skips' confirmed graph (WP-053 task 2): identical to the platform default,
    /// except AWAITING_REVIEW -> APPROVED is removed, plus the five
    /// CHECKED_READY_TO_APPROVE / NEEDS_REVIEW_FEBINA rows.
    /// </summary>
    private static IEnumerable<TransitionSeedRow> BuildGbSkipsTransitions()
    {
        var templateId = WorkflowSeedData.GbSkipsTemplateId;

        foreach (var row in BuildSharedTransitions(templateId))
        {
            yield return row;
        }

        // Standard/Reviewer checks the invoice and marks it ready for approval.
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.AwaitingReview, InvoiceStatusCodes.CheckedReadyToApprove);

        // Full/Approver approves - ROLE GATED (see RoleGatedTransitions).
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.CheckedReadyToApprove, InvoiceStatusCodes.Approved);

        // Escalation to Febina, from either pre-approval review state.
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.AwaitingReview, InvoiceStatusCodes.NeedsReviewFebina);
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.CheckedReadyToApprove, InvoiceStatusCodes.NeedsReviewFebina);

        // Escalation resolution outcomes.
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.NeedsReviewFebina, InvoiceStatusCodes.CheckedReadyToApprove);
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.NeedsReviewFebina, InvoiceStatusCodes.NeedsQuery);
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.NeedsReviewFebina, InvoiceStatusCodes.Rejected);

        // Full/Approver raises a query rather than approving - ROLE GATED, described
        // in WP-053's table as "Full/Approver, no self-escalation".
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.CheckedReadyToApprove, InvoiceStatusCodes.NeedsQuery);
    }

    /// <summary>
    /// The transitions both templates share - everything in WP-053 task 1's table
    /// except AWAITING_REVIEW -> APPROVED (platform-default only; see task 2's
    /// "identical to the above, except...").
    /// </summary>
    private static IEnumerable<TransitionSeedRow> BuildSharedTransitions(Guid templateId)
    {
        // Ingestion pipeline progression (system-driven).
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.Received, InvoiceStatusCodes.Processing);
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.Processing, InvoiceStatusCodes.Extracted);
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.Extracted, InvoiceStatusCodes.AwaitingReview);

        // Query cycle.
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.AwaitingReview, InvoiceStatusCodes.NeedsQuery);
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.NeedsQuery, InvoiceStatusCodes.QueryRaised);
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.QueryRaised, InvoiceStatusCodes.AwaitingSupplierResponse);
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.AwaitingSupplierResponse, InvoiceStatusCodes.AwaitingReview);

        // Rejection, from any reviewable state.
        foreach (var from in RejectableStatuses)
        {
            yield return new TransitionSeedRow(templateId, from, InvoiceStatusCodes.Rejected);
        }

        // Manual cancellation, from any pre-decision state.
        foreach (var from in CancellableStatuses)
        {
            yield return new TransitionSeedRow(templateId, from, InvoiceStatusCodes.Cancelled);
        }

        // Reopen paths - both ROLE GATED (see RoleGatedTransitions).
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.Rejected, InvoiceStatusCodes.AwaitingReview);
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.Cancelled, InvoiceStatusCodes.Received);

        // Payment lifecycle.
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.Approved, InvoiceStatusCodes.ReadyForPayment);
        yield return new TransitionSeedRow(templateId, InvoiceStatusCodes.ReadyForPayment, InvoiceStatusCodes.Paid);

        // Archival for retention.
        foreach (var from in ArchivableStatuses)
        {
            yield return new TransitionSeedRow(templateId, from, InvoiceStatusCodes.Archived);
        }
    }

    /// <summary>
    /// Builds a deterministic, stable <see cref="Guid"/> for a transition from its
    /// own identity (template + from + to), so EF Core's <c>HasData</c> produces the
    /// same INSERT statements on every migration generation. Uses MD5 purely as a
    /// deterministic 128-bit mixing function to fit a string triple into a Guid -
    /// NOT for any security purpose (no secret, no authentication, no integrity
    /// claim), so its cryptographic weakness is irrelevant here.
    /// </summary>
    private static Guid BuildId(Guid templateId, string fromStatusCode, string toStatusCode)
    {
        var key = $"{templateId}|{fromStatusCode}|{toStatusCode}";
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(key));
        return new Guid(hash);
    }
}
