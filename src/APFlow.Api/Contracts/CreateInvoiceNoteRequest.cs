namespace APFlow.Api.Contracts;

/// <summary>
/// Request shape for <c>POST /api/invoices/{id}/notes</c> (WP-055). Content
/// validation (empty/over-length) is performed by the existing
/// <c>IInvoiceService.AddNoteAsync</c> (WP-009) - not duplicated here.
/// </summary>
public sealed record CreateInvoiceNoteRequest(string Content);
