using APFlow.Application.DTOs;
using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// CRUD orchestration for invoices. WP-009 scope only: create/read/update/delete and
/// note-taking. No query/dispute workflow, no remittance handling, no approval
/// workflow - all explicit WP-009 out-of-scope items, deferred to future work
/// packages. Duplicate detection (WP-010) is intentionally a separate service - see
/// <see cref="IDuplicateDetectionService"/> - rather than folded in here, so this
/// interface stays focused on plain CRUD.
/// </summary>
public interface IInvoiceService
{
    /// <summary>Returns the invoice with the given id.</summary>
    Task<Result<InvoiceDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns every invoice visible to the current tenant.</summary>
    Task<Result<IReadOnlyList<InvoiceDto>>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a new invoice against an existing supplier.</summary>
    Task<Result<InvoiceDto>> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing invoice's editable fields.</summary>
    Task<Result<InvoiceDto>> UpdateAsync(Guid id, UpdateInvoiceRequest request, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes an invoice.</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a freeform note to an invoice (see <c>InvoiceNote</c>'s doc comment for
    /// how this differs from the out-of-scope query/dispute workflow). Returns the
    /// created note (id, content, resolved author display name, timestamp) - WP-017
    /// ruling, 2026-07-25 - so a <c>POST .../notes</c> caller can shape a proper
    /// <c>201 Created</c> response without a second read.
    /// </summary>
    Task<Result<InvoiceNoteDto>> AddNoteAsync(Guid invoiceId, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every note recorded against an invoice, oldest first (WP-017's
    /// ruled chronological order - see docs/WP-017-Invoice-Notes-Decisions.md
    /// item 2). Returns an <c>Invoice.NotFound</c> failure if the invoice does
    /// not exist or is not visible to the current tenant.
    /// </summary>
    Task<Result<IReadOnlyList<InvoiceNoteDto>>> GetNotesAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}
