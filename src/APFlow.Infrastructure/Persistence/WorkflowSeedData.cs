using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;

namespace APFlow.Infrastructure.Persistence;

/// <summary>
/// Fixed, deterministic data for WP-050's platform-default and GB Skips
/// <see cref="WorkflowTemplate"/>/<see cref="StatusReference"/> seed rows, used by
/// <c>WorkflowTemplateConfiguration</c>/<c>StatusReferenceConfiguration</c>'s
/// <c>HasData</c> calls. Fixed (not <c>Guid.CreateVersion7()</c>-generated) because
/// EF Core's <c>HasData</c> seeding is baked into a migration and must produce the
/// same INSERT statements every time it's generated/applied - see
/// docs/WP-050-Workflow-Engine-Decisions.md.
/// No transitions are seeded here - see <see cref="WorkflowTransition"/>'s doc
/// comment for why.
/// </summary>
public static class WorkflowSeedData
{
    /// <summary>
    /// PLACEHOLDER - not GB Skips' real Entra tenant id (the "tid" claim value).
    /// This codebase has no real GB Skips tenant configured anywhere (confirmed:
    /// no App Registration, no verified tenant id exists in any doc this project
    /// has produced - see docs/WP-002-Entra-Verification-Checklist.md,
    /// docs/WP-004-Graph-Verification-Checklist.md for the same "no real tenant
    /// available yet" situation at every prior point this has come up). This value
    /// MUST be corrected to GB Skips' real Entra tenant id before this template can
    /// ever actually apply to a real GB Skips user - until then, the GB Skips
    /// template exists in the database but is unreachable (no real
    /// ICurrentUserService.TenantId will ever equal this placeholder).
    /// </summary>
    public static readonly Guid GbSkipsPlaceholderTenantId = Guid.Parse("00000000-0000-0000-0000-0000000B5121");

    /// <summary>Fixed id for the platform-default template's seed row.</summary>
    public static readonly Guid PlatformDefaultTemplateId = Guid.Parse("00000000-0000-0000-0001-000000000001");

    /// <summary>Fixed id for GB Skips' template seed row.</summary>
    public static readonly Guid GbSkipsTemplateId = Guid.Parse("00000000-0000-0000-0001-000000000002");

    /// <summary>Seed timestamp used for every row's CreatedAtUtc - arbitrary but fixed, so the migration's generated INSERT statements are stable/reproducible.</summary>
    public static readonly DateTimeOffset SeedTimestamp = new(2026, 7, 23, 0, 0, 0, TimeSpan.Zero);

    /// <summary>One seed row for a <see cref="StatusReference"/>.</summary>
    /// <param name="Id">Fixed id for this row.</param>
    /// <param name="TemplateId">The owning template's id.</param>
    /// <param name="Code">The status code - see <see cref="InvoiceStatusCodes"/>.</param>
    /// <param name="Name">The human-readable name.</param>
    /// <param name="IsTerminal">Whether this status is terminal.</param>
    /// <param name="SortOrder">Relative display/lifecycle ordering.</param>
    public sealed record StatusSeedRow(Guid Id, Guid TemplateId, string Code, string Name, bool IsTerminal, int SortOrder);

    /// <summary>
    /// The platform-default catalogue - docs/06_Domain_Reference_Data.md §2,
    /// unchanged from SA-002 §5 (task 2). SortOrder matches that document's table
    /// order. "EXTRACTED" (WP-008/WP-012's pipeline output status) is included
    /// even though it is NOT listed in 06_Domain_Reference_Data.md §2 - flagged
    /// explicitly in docs/WP-050-Workflow-Engine-Decisions.md as a pre-existing
    /// discrepancy this WP surfaces rather than silently resolves; it is kept
    /// because removing it would break the already-shipped, tested WP-012/WP-049
    /// ingestion pipeline, which relies on it as a real status.
    /// </summary>
    public static readonly IReadOnlyList<StatusSeedRow> PlatformDefaultStatuses =
    [
        new(Guid.Parse("00000000-0000-0000-0002-000000000001"), PlatformDefaultTemplateId, InvoiceStatusCodes.Received, "Received", false, 10),
        new(Guid.Parse("00000000-0000-0000-0002-000000000002"), PlatformDefaultTemplateId, InvoiceStatusCodes.Extracted, "Extracted", false, 15),
        new(Guid.Parse("00000000-0000-0000-0002-000000000003"), PlatformDefaultTemplateId, InvoiceStatusCodes.Processing, "Processing", false, 20),
        new(Guid.Parse("00000000-0000-0000-0002-000000000004"), PlatformDefaultTemplateId, InvoiceStatusCodes.DuplicateSuspected, "Duplicate Suspected", false, 30),
        new(Guid.Parse("00000000-0000-0000-0002-000000000005"), PlatformDefaultTemplateId, InvoiceStatusCodes.AwaitingReview, "Awaiting Review", false, 40),
        new(Guid.Parse("00000000-0000-0000-0002-000000000006"), PlatformDefaultTemplateId, InvoiceStatusCodes.NeedsQuery, "Needs Query", false, 50),
        new(Guid.Parse("00000000-0000-0000-0002-000000000007"), PlatformDefaultTemplateId, InvoiceStatusCodes.QueryRaised, "Query Raised", false, 60),
        new(Guid.Parse("00000000-0000-0000-0002-000000000008"), PlatformDefaultTemplateId, InvoiceStatusCodes.AwaitingSupplierResponse, "Awaiting Supplier Response", false, 70),
        new(Guid.Parse("00000000-0000-0000-0002-000000000009"), PlatformDefaultTemplateId, InvoiceStatusCodes.Approved, "Approved", false, 80),
        new(Guid.Parse("00000000-0000-0000-0002-00000000000a"), PlatformDefaultTemplateId, InvoiceStatusCodes.Rejected, "Rejected", false, 90),
        new(Guid.Parse("00000000-0000-0000-0002-00000000000b"), PlatformDefaultTemplateId, InvoiceStatusCodes.Cancelled, "Cancelled", false, 100),
        new(Guid.Parse("00000000-0000-0000-0002-00000000000c"), PlatformDefaultTemplateId, InvoiceStatusCodes.ReadyForPayment, "Ready for Payment", false, 110),
        new(Guid.Parse("00000000-0000-0000-0002-00000000000d"), PlatformDefaultTemplateId, InvoiceStatusCodes.Paid, "Paid", false, 120),
        new(Guid.Parse("00000000-0000-0000-0002-00000000000e"), PlatformDefaultTemplateId, InvoiceStatusCodes.Archived, "Archived", true, 130),
    ];

    /// <summary>
    /// GB Skips' full status list (task 3): every platform-default status PLUS the
    /// two tenant-specific additions, positioned between AWAITING_REVIEW (40) and
    /// APPROVED (80) via SortOrder (45/46) - matching task 3's "positioned between"
    /// requirement without renumbering the platform-default rows. A tenant-specific
    /// template is a COMPLETE replacement (task 2: "this remains what every tenant
    /// gets unless they have their own template" implies an own template is used in
    /// full, not merged with the platform default), so GB Skips' template repeats
    /// the full baseline list under its own TemplateId/row ids rather than
    /// referencing the platform-default rows.
    /// </summary>
    public static readonly IReadOnlyList<StatusSeedRow> GbSkipsStatuses =
    [
        new(Guid.Parse("00000000-0000-0000-0003-000000000001"), GbSkipsTemplateId, InvoiceStatusCodes.Received, "Received", false, 10),
        new(Guid.Parse("00000000-0000-0000-0003-000000000002"), GbSkipsTemplateId, InvoiceStatusCodes.Extracted, "Extracted", false, 15),
        new(Guid.Parse("00000000-0000-0000-0003-000000000003"), GbSkipsTemplateId, InvoiceStatusCodes.Processing, "Processing", false, 20),
        new(Guid.Parse("00000000-0000-0000-0003-000000000004"), GbSkipsTemplateId, InvoiceStatusCodes.DuplicateSuspected, "Duplicate Suspected", false, 30),
        new(Guid.Parse("00000000-0000-0000-0003-000000000005"), GbSkipsTemplateId, InvoiceStatusCodes.AwaitingReview, "Awaiting Review", false, 40),
        new(Guid.Parse("00000000-0000-0000-0003-000000000006"), GbSkipsTemplateId, InvoiceStatusCodes.CheckedReadyToApprove, "Checked & Ready to Approve", false, 45),
        new(Guid.Parse("00000000-0000-0000-0003-000000000007"), GbSkipsTemplateId, InvoiceStatusCodes.NeedsReviewFebina, "Needs Review by Febina", false, 46),
        new(Guid.Parse("00000000-0000-0000-0003-000000000008"), GbSkipsTemplateId, InvoiceStatusCodes.NeedsQuery, "Needs Query", false, 50),
        new(Guid.Parse("00000000-0000-0000-0003-000000000009"), GbSkipsTemplateId, InvoiceStatusCodes.QueryRaised, "Query Raised", false, 60),
        new(Guid.Parse("00000000-0000-0000-0003-00000000000a"), GbSkipsTemplateId, InvoiceStatusCodes.AwaitingSupplierResponse, "Awaiting Supplier Response", false, 70),
        new(Guid.Parse("00000000-0000-0000-0003-00000000000b"), GbSkipsTemplateId, InvoiceStatusCodes.Approved, "Approved", false, 80),
        new(Guid.Parse("00000000-0000-0000-0003-00000000000c"), GbSkipsTemplateId, InvoiceStatusCodes.Rejected, "Rejected", false, 90),
        new(Guid.Parse("00000000-0000-0000-0003-00000000000d"), GbSkipsTemplateId, InvoiceStatusCodes.Cancelled, "Cancelled", false, 100),
        new(Guid.Parse("00000000-0000-0000-0003-00000000000e"), GbSkipsTemplateId, InvoiceStatusCodes.ReadyForPayment, "Ready for Payment", false, 110),
        new(Guid.Parse("00000000-0000-0000-0003-00000000000f"), GbSkipsTemplateId, InvoiceStatusCodes.Paid, "Paid", false, 120),
        new(Guid.Parse("00000000-0000-0000-0003-000000000010"), GbSkipsTemplateId, InvoiceStatusCodes.Archived, "Archived", true, 130),
    ];
}
