namespace APFlow.Api.Contracts;

/// <summary>
/// Request shape for <c>POST /api/invoices/{id}/notes</c> (WP-017 ruling,
/// 2026-07-25). Deliberately just the note text - author identity is resolved
/// server-side from the caller's own validated token
/// (<c>ICurrentUserService.DisplayName</c>), never client-supplied.
/// </summary>
public sealed record CreateInvoiceNoteRequest(string Content);
