namespace APFlow.Api.Contracts;

/// <summary>
/// Request shape for <c>PATCH /api/invoices/{id}/status</c> (WP-054 task 2).
/// <see cref="Notes"/> is generic (not per-transition typed) - "sufficient for
/// MVP" per task 4; richer per-action fields (e.g. a structured query reason)
/// remain WP-031's concern if/when that is built. Nullable here purely because
/// this is a wire-format binding type, but WP-084 makes it required in
/// practice for any request that actually changes the invoice's status:
/// <c>InvoiceService.UpdateAsync</c> rejects a missing/empty value with
/// <c>400 Invoice.NoteRequiredForTransition</c> before touching the invoice at
/// all, and creates the linked <c>InvoiceNote</c> atomically with the status
/// change (via the same freeform note mechanism WP-009 exposes) when present -
/// not a separate, independently-failable follow-up call any more.
/// </summary>
public sealed record UpdateInvoiceStatusRequest(string TargetStatusCode, string? Notes);
