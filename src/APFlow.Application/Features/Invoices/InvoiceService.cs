using APFlow.Application.Common;
using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace APFlow.Application.Features.Invoices;

/// <summary>
/// Default implementation of <see cref="IInvoiceService"/>. Depends on
/// <see cref="IInvoiceRepository"/>, <see cref="ISupplierRepository"/>, (WP-013)
/// <see cref="IAuditService"/>, and (WP-051) <see cref="ICurrentUserService"/> /
/// <see cref="IApprovalAuthorizationService"/> - all plain, EF-Core-free
/// interfaces - so this class is fully unit-testable with fake
/// repositories/services. No database, no EF Core provider required.
/// </summary>
public sealed class InvoiceService : IInvoiceService
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly IAuditService _auditService;
    private readonly ICurrentUserService _currentUserService;
    private readonly IApprovalAuthorizationService _approvalAuthorizationService;
    private readonly ILogger<InvoiceService> _logger;

    /// <summary>Creates a new <see cref="InvoiceService"/>.</summary>
    public InvoiceService(
        IInvoiceRepository invoiceRepository,
        ISupplierRepository supplierRepository,
        IAuditService auditService,
        ICurrentUserService currentUserService,
        IApprovalAuthorizationService approvalAuthorizationService,
        ILogger<InvoiceService> logger)
    {
        _invoiceRepository = invoiceRepository;
        _supplierRepository = supplierRepository;
        _auditService = auditService;
        _currentUserService = currentUserService;
        _approvalAuthorizationService = approvalAuthorizationService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<InvoiceDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(id, cancellationToken);

        return invoice is null
            ? Result.Failure<InvoiceDto>(new Error("Invoice.NotFound", $"Invoice '{id}' was not found."))
            : Result.Success(ToDto(invoice));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<InvoiceDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var invoices = await _invoiceRepository.GetAllAsync(cancellationToken);
        return Result.Success<IReadOnlyList<InvoiceDto>>(invoices.Select(ToDto).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<InvoiceDto>> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = InvoiceFieldValidation.Validate(request.SupplierInvoiceNumber, request.Currency);
        if (validationError is not null)
        {
            return Result.Failure<InvoiceDto>(validationError);
        }

        var supplier = await _supplierRepository.GetByIdAsync(request.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure<InvoiceDto>(
                new Error("Invoice.SupplierNotFound", $"Supplier '{request.SupplierId}' was not found."));
        }

        var invoice = new Invoice
        {
            SupplierId = request.SupplierId,
            SupplierInvoiceNumber = request.SupplierInvoiceNumber,
            InvoiceDate = request.InvoiceDate,
            DueDate = request.DueDate,
            Currency = request.Currency,
            NetAmount = request.NetAmount,
            Vat = request.Vat,
            GrossTotal = request.GrossTotal,
            SourceEmailMessageId = request.SourceEmailMessageId,
            SourceDocumentBlobName = request.SourceDocumentBlobName,
            SourceDocumentContentHash = request.SourceDocumentContentHash,
        };

        await _invoiceRepository.AddAsync(invoice, cancellationToken);

        // WP-052 Part C: automatically log invoice creation. Staged only (per
        // WP-013's "stage, don't save" pattern - see IAuditService.LogAsync's doc
        // comment) so it commits atomically with the invoice's own insert via the
        // single SaveChangesAsync call below, not as an independent commit.
        var createAuditResult = await _auditService.LogAsync(
            new RecordAuditLogRequest(
                Action: AuditActions.InvoiceCreated,
                EntityName: nameof(Invoice),
                EntityId: invoice.Id,
                PreviousValue: null,
                NewValue: SerializeSnapshot(invoice, supplier.Name)),
            cancellationToken);

        if (createAuditResult.IsFailure)
        {
            // Same reasoning as UpdateAsync's status-change audit failure handling:
            // a missing audit entry is a smaller problem than refusing a
            // legitimate creation because of it. Logged loudly, not silent.
            _logger.LogWarning(
                "Failed to stage audit log entry for invoice {InvoiceId} creation: {ErrorCode} - {ErrorMessage}",
                invoice.Id, createAuditResult.Error.Code, createAuditResult.Error.Message);
        }

        await _invoiceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created invoice {InvoiceId} for supplier {SupplierId}.", invoice.Id, invoice.SupplierId);

        invoice.Supplier = supplier;
        return Result.Success(ToDto(invoice));
    }

    /// <inheritdoc />
    public async Task<Result<InvoiceDto>> UpdateAsync(Guid id, UpdateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = InvoiceFieldValidation.Validate(request.SupplierInvoiceNumber, request.Currency);
        if (validationError is not null)
        {
            return Result.Failure<InvoiceDto>(validationError);
        }

        var invoice = await _invoiceRepository.GetByIdAsync(id, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure<InvoiceDto>(new Error("Invoice.NotFound", $"Invoice '{id}' was not found."));
        }

        var previousStatus = invoice.Status;

        // WP-051 task 4: gate the CHECKED_READY_TO_APPROVE -> APPROVED transition
        // specifically by the acting user's role, via the seeded GB Skips
        // ApprovalPolicy (ApprovalDomains.InvoiceApproval). Deliberately narrow -
        // NOT a general "check every transition against IWorkflowValidationService"
        // activation, which remains blocked on the platform-default transition
        // graph being undocumented anywhere (see docs/WP-050-Workflow-Engine-Decisions.md).
        // This check only ever matters for GB Skips tenants: the platform-default
        // template has no CHECKED_READY_TO_APPROVE status at all, so
        // previousStatus can never equal it for a platform-default invoice.
        // Checked and rejected BEFORE any field is mutated, so an unauthorized
        // attempt leaves the invoice completely untouched.
        if (string.Equals(previousStatus, InvoiceStatusCodes.CheckedReadyToApprove, StringComparison.Ordinal)
            && string.Equals(request.Status, InvoiceStatusCodes.Approved, StringComparison.Ordinal))
        {
            var authorizationResult = await _approvalAuthorizationService.AuthorizeAsync(
                ApprovalDomains.InvoiceApproval, _currentUserService.Roles, cancellationToken);

            if (authorizationResult.IsFailure)
            {
                _logger.LogWarning(
                    "Invoice {InvoiceId} approval rejected: {ErrorCode} - {ErrorMessage}",
                    invoice.Id, authorizationResult.Error.Code, authorizationResult.Error.Message);
                return Result.Failure<InvoiceDto>(authorizationResult.Error);
            }
        }

        invoice.SupplierInvoiceNumber = request.SupplierInvoiceNumber;
        invoice.InvoiceDate = request.InvoiceDate;
        invoice.DueDate = request.DueDate;
        invoice.Currency = request.Currency;
        invoice.NetAmount = request.NetAmount;
        invoice.Vat = request.Vat;
        invoice.GrossTotal = request.GrossTotal;
        invoice.Status = request.Status;

        _invoiceRepository.Update(invoice);

        // WP-013 task 4: automatically log invoice status changes. Centralized here
        // (rather than in each individual caller, e.g. WP-012's pipeline) so every
        // current and future caller of UpdateAsync gets this for free and cannot
        // forget it - same "enforce centrally" pattern as WP-005's blob tenant
        // prefixing. LogAsync only stages the entry; it is committed together with
        // the invoice update itself by the single SaveChangesAsync call below - see
        // IAuditService.LogAsync's doc comment for why that matters.
        if (previousStatus != request.Status)
        {
            var auditResult = await _auditService.LogAsync(
                new RecordAuditLogRequest(
                    Action: AuditActions.InvoiceStatusChanged,
                    EntityName: nameof(Invoice),
                    EntityId: invoice.Id,
                    PreviousValue: previousStatus,
                    NewValue: request.Status),
                cancellationToken);

            if (auditResult.IsFailure)
            {
                // The status change itself is not blocked by an audit-staging
                // failure (e.g. a validation bug in a future caller's request) - a
                // missing audit entry is a smaller problem than refusing a
                // legitimate business update because of it. Logged loudly so the
                // gap is visible, not silent.
                _logger.LogWarning(
                    "Failed to stage audit log entry for invoice {InvoiceId} status change ({PreviousStatus} -> {NewStatus}): {ErrorCode} - {ErrorMessage}",
                    invoice.Id, previousStatus, request.Status, auditResult.Error.Code, auditResult.Error.Message);
            }
        }

        await _invoiceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated invoice {InvoiceId}. Status={Status}.", invoice.Id, invoice.Status);

        return Result.Success(ToDto(invoice));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(id, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure(new Error("Invoice.NotFound", $"Invoice '{id}' was not found."));
        }

        _invoiceRepository.Remove(invoice);

        // WP-052 Part C: automatically log invoice deletion. Snapshot captured
        // BEFORE Remove takes effect (the in-memory entity is unchanged by
        // Remove() until SaveChangesAsync commits - see FakeInvoiceRepository's
        // own no-op Update()/Remove() semantics for the same reasoning applied to
        // tests). Staged only, commits atomically with the deletion itself.
        var deleteAuditResult = await _auditService.LogAsync(
            new RecordAuditLogRequest(
                Action: AuditActions.InvoiceDeleted,
                EntityName: nameof(Invoice),
                EntityId: invoice.Id,
                PreviousValue: SerializeSnapshot(invoice, invoice.Supplier?.Name),
                NewValue: null),
            cancellationToken);

        if (deleteAuditResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to stage audit log entry for invoice {InvoiceId} deletion: {ErrorCode} - {ErrorMessage}",
                invoice.Id, deleteAuditResult.Error.Code, deleteAuditResult.Error.Message);
        }

        await _invoiceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted (soft) invoice {InvoiceId}.", id);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> AddNoteAsync(Guid invoiceId, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Result.Failure(new Error("Invoice.InvalidNoteContent", "Note content must not be empty."));
        }

        if (content.Length > FieldLimits.InvoiceNoteContent)
        {
            return Result.Failure(new Error(
                "Invoice.InvalidNoteContent",
                $"Note content must not exceed {FieldLimits.InvoiceNoteContent} characters."));
        }

        var invoice = await _invoiceRepository.GetByIdWithNotesAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure(new Error("Invoice.NotFound", $"Invoice '{invoiceId}' was not found."));
        }

        invoice.Notes.Add(new InvoiceNote
        {
            InvoiceId = invoiceId,
            Content = content,
        });

        _invoiceRepository.Update(invoice);

        // WP-052 Part C: automatically log note additions. NewValue is the raw
        // note content itself (per this task's literal wording), not a JSON
        // snapshot - unlike Create/Delete, there is no multi-field entity state to
        // capture, just the one piece of new information. Staged only, commits
        // atomically with the note's own insert.
        var noteAuditResult = await _auditService.LogAsync(
            new RecordAuditLogRequest(
                Action: AuditActions.NoteAdded,
                EntityName: nameof(Invoice),
                EntityId: invoiceId,
                PreviousValue: null,
                NewValue: content),
            cancellationToken);

        if (noteAuditResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to stage audit log entry for note added to invoice {InvoiceId}: {ErrorCode} - {ErrorMessage}",
                invoiceId, noteAuditResult.Error.Code, noteAuditResult.Error.Message);
        }

        await _invoiceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Added note to invoice {InvoiceId}.", invoiceId);

        return Result.Success();
    }

    /// <summary>
    /// Builds the JSON snapshot used for the InvoiceCreated/InvoiceDeleted audit
    /// entries (WP-052 Part C): Supplier, Invoice Number, Invoice Date, Gross
    /// Amount, Currency, and Status - the fields task C names explicitly.
    /// Deliberately the same shape for both create and delete, so a reviewer
    /// comparing a deletion's PreviousValue against the corresponding creation's
    /// NewValue is looking at directly comparable data.
    /// </summary>
    private static string SerializeSnapshot(Invoice invoice, string? supplierName) =>
        System.Text.Json.JsonSerializer.Serialize(new InvoiceAuditSnapshot(
            invoice.SupplierId, supplierName, invoice.SupplierInvoiceNumber, invoice.InvoiceDate,
            invoice.GrossTotal, invoice.Currency, invoice.Status));

    private sealed record InvoiceAuditSnapshot(
        Guid SupplierId,
        string? SupplierName,
        string? SupplierInvoiceNumber,
        DateOnly? InvoiceDate,
        decimal? GrossTotal,
        string? Currency,
        string Status);

    private static InvoiceDto ToDto(Invoice invoice) => new(
        Id: invoice.Id,
        SupplierId: invoice.SupplierId,
        SupplierName: invoice.Supplier?.Name,
        SupplierInvoiceNumber: invoice.SupplierInvoiceNumber,
        InvoiceDate: invoice.InvoiceDate,
        DueDate: invoice.DueDate,
        Currency: invoice.Currency,
        NetAmount: invoice.NetAmount,
        Vat: invoice.Vat,
        GrossTotal: invoice.GrossTotal,
        Status: invoice.Status,
        SourceEmailMessageId: invoice.SourceEmailMessageId,
        SourceDocumentBlobName: invoice.SourceDocumentBlobName,
        SourceDocumentContentHash: invoice.SourceDocumentContentHash,
        IsPotentialDuplicate: invoice.IsPotentialDuplicate,
        DuplicateCheckReason: invoice.DuplicateCheckReason,
        CreatedAtUtc: invoice.CreatedAtUtc);
}
