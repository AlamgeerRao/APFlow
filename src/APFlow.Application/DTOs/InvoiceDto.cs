namespace APFlow.Application.DTOs;

/// <summary>Read shape for an invoice.</summary>
public sealed record InvoiceDto(
    Guid Id,
    Guid SupplierId,
    string? SupplierName,
    string? SupplierInvoiceNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    string? Currency,
    decimal? NetAmount,
    decimal? Vat,
    decimal? GrossTotal,
    string Status,
    string? SourceEmailMessageId,
    string? SourceDocumentBlobName,
    string? SourceDocumentContentHash,
    bool IsPotentialDuplicate,
    string? DuplicateCheckReason,
    // WP-073: the matched existing invoice's id, if any - see Invoice.DuplicateMatchInvoiceId.
    Guid? DuplicateMatchInvoiceId,
    DateTimeOffset CreatedAtUtc);

/// <summary>Request shape for creating an invoice. No Id, no Status (always starts at InvoiceStatusCodes.Received), no audit fields - those are owned by the entity/AppDbContext.</summary>
public sealed record CreateInvoiceRequest(
    Guid SupplierId,
    string? SupplierInvoiceNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    string? Currency,
    decimal? NetAmount,
    decimal? Vat,
    decimal? GrossTotal,
    string? SourceEmailMessageId,
    string? SourceDocumentBlobName = null,
    string? SourceDocumentContentHash = null);

/// <summary>
/// Request shape for updating an invoice's editable fields, including
/// <see cref="Status"/>. Transition validity and role-gating are enforced by
/// <c>IInvoiceService.UpdateAsync</c> itself as of WP-053 - this DTO carries
/// the request, it does not describe what's allowed.
/// <see cref="Notes"/> (WP-084) is required whenever <see cref="Status"/>
/// actually differs from the invoice's current status - a human-initiated
/// transition with no accompanying note is rejected before any field is
/// mutated. Ignored (and not required) when the status is unchanged, since
/// that is not a transition.
/// </summary>
public sealed record UpdateInvoiceRequest(
    string? SupplierInvoiceNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    string? Currency,
    decimal? NetAmount,
    decimal? Vat,
    decimal? GrossTotal,
    string Status,
    string? Notes = null);
