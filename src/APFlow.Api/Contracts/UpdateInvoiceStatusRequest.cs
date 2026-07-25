namespace APFlow.Api.Contracts;

/// <summary>
/// Request shape for <c>PATCH /api/invoices/{id}/status</c> (WP-054 task 2).
/// <see cref="Notes"/> is deliberately optional and generic (not per-transition
/// typed) - "sufficient for MVP" per task 4; richer per-action fields (e.g. a
/// structured query reason) remain WP-031's concern if/when that is built. When
/// present and non-empty, it is recorded via <c>IInvoiceService.AddNoteAsync</c> -
/// the same freeform note mechanism WP-009 already exposes, not a new one.
/// </summary>
public sealed record UpdateInvoiceStatusRequest(string TargetStatusCode, string? Notes);
