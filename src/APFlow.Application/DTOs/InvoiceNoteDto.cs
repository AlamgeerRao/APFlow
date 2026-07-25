namespace APFlow.Application.DTOs;

/// <summary>
/// Read shape for a single invoice note (WP-055). Used both for the list returned
/// by <c>GET /api/invoices/{id}/notes</c> and for the single created note returned
/// by <c>POST /api/invoices/{id}/notes</c>, so both endpoints share one shape
/// rather than two subtly different ones.
/// </summary>
public sealed record InvoiceNoteDto(
    Guid Id,
    string Content,
    string? AuthorDisplayName,
    DateTimeOffset CreatedAtUtc);
