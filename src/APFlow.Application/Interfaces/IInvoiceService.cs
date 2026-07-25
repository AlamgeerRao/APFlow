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
    /// Returns every note recorded against an invoice, most recent first (WP-055) -
    /// exposes <c>IInvoiceRepository.GetByIdWithNotesAsync</c> (WP-009) as a plain
    /// read shape.
    /// </summary>
    Task<Result<IReadOnlyList<InvoiceNoteDto>>> GetNotesAsync(Guid invoiceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a freeform note to an invoice (see <c>InvoiceNote</c>'s doc comment for
    /// how this differs from the out-of-scope query/dispute workflow). Returns the
    /// created note (WP-055) - including its server-assigned
    /// <see cref="APFlow.Domain.Entities.BaseEntity.Id"/> and
    /// <see cref="APFlow.Domain.Entities.AuditEntity.CreatedAtUtc"/> - so a caller
    /// (e.g. the WP-055 <c>POST</c> endpoint) can shape a <c>201 Created</c>
    /// response without a second round-trip to re-fetch it.
    /// </summary>
    Task<Result<InvoiceNoteDto>> AddNoteAsync(Guid invoiceId, string content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the Document Intelligence per-field extraction confidence data
    /// persisted for an invoice (WP-056), in
    /// <see cref="APFlow.Domain.Common.Constants.InvoiceExtractedFieldKeys.CanonicalOrder"/> -
    /// exposes <c>IInvoiceRepository.GetByIdWithExtractedFieldsAsync</c> as a
    /// plain read shape. Empty (not a failure) for an invoice created before
    /// WP-056, or one not processed via the WP-012 pipeline at all (e.g. created
    /// manually) - the absence of extraction data is a normal, expected state,
    /// not an error.
    /// </summary>
    Task<Result<IReadOnlyList<InvoiceExtractedFieldDto>>> GetExtractedFieldsAsync(Guid invoiceId, CancellationToken cancellationToken = default);
}
