namespace APFlow.Application.DTOs;

/// <summary>
/// Read shape for a single freeform note recorded against an invoice (WP-009's
/// <c>InvoiceNote</c>). <see cref="AuthorDisplayName"/> is resolved server-side
/// at write time (WP-017 ruling, 2026-07-25) rather than exposing the raw
/// <c>CreatedBy</c> identifier - see <c>InvoiceNote.AuthorDisplayName</c>'s own
/// doc comment.
/// </summary>
public sealed record InvoiceNoteDto(
    Guid Id,
    string Content,
    string? AuthorDisplayName,
    DateTimeOffset CreatedAtUtc);
