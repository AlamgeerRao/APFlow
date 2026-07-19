namespace APFlow.Application.DTOs;

/// <summary>
/// Outcome of a duplicate check performed against a single invoice. Advisory only -
/// producing this result never changes the invoice itself in any way. Per WP-010
/// ("Do not automatically reject invoices"), the caller decides what to do with a
/// flagged result; this type only reports what was found.
/// </summary>
public sealed record DuplicateCheckResult(
    Guid InvoiceId,
    bool IsPotentialDuplicate,
    IReadOnlyList<DuplicateMatch> Matches);

/// <summary>
/// A single other invoice that the checked invoice potentially duplicates, together
/// with which comparison fields matched and a human-readable reason recording why
/// it was flagged.
/// </summary>
public sealed record DuplicateMatch(
    Guid MatchedInvoiceId,
    string? MatchedSupplierInvoiceNumber,
    IReadOnlyList<string> MatchedFields,
    string Reason);
