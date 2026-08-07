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
/// <see cref="IAuditService"/>, (WP-051) <see cref="ICurrentUserService"/> /
/// <see cref="IApprovalAuthorizationService"/>, and (WP-053)
/// <see cref="IWorkflowValidationService"/> - all plain, EF-Core-free
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
    private readonly IWorkflowValidationService _workflowValidationService;
    private readonly IEmailSendService _emailSendService;
    private readonly ILogger<InvoiceService> _logger;

    /// <summary>Creates a new <see cref="InvoiceService"/>.</summary>
    public InvoiceService(
        IInvoiceRepository invoiceRepository,
        ISupplierRepository supplierRepository,
        IAuditService auditService,
        ICurrentUserService currentUserService,
        IApprovalAuthorizationService approvalAuthorizationService,
        IWorkflowValidationService workflowValidationService,
        IEmailSendService emailSendService,
        ILogger<InvoiceService> logger)
    {
        _invoiceRepository = invoiceRepository;
        _supplierRepository = supplierRepository;
        _auditService = auditService;
        _currentUserService = currentUserService;
        _approvalAuthorizationService = approvalAuthorizationService;
        _workflowValidationService = workflowValidationService;
        _emailSendService = emailSendService;
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

        // WP-084: loaded with Notes now (not plain GetByIdAsync) - a status-changing
        // request stages a linked InvoiceNote directly onto this same tracked
        // instance further down, committed by the one SaveChangesAsync call at the
        // end of this method, not a separate save.
        var invoice = await _invoiceRepository.GetByIdWithNotesAsync(id, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure<InvoiceDto>(new Error("Invoice.NotFound", $"Invoice '{id}' was not found."));
        }

        var previousStatus = invoice.Status;
        var statusIsChanging = !string.Equals(previousStatus, request.Status, StringComparison.Ordinal);

        // WP-053: enforcement is now LIVE. Every check in this block runs before any
        // field is mutated, so a rejected attempt leaves the invoice completely
        // untouched. Only run at all when the status is actually changing - a plain
        // field edit (same status) is not a transition and is neither validated nor
        // gated.
        if (statusIsChanging)
        {
            // (1) Is this transition allowed at all by the acting tenant's workflow
            // template? WP-050 built and fully tested IWorkflowValidationService but
            // deliberately never called it from a blocking path, because neither
            // template had a confirmed transition graph seeded - doing so then would
            // have rejected every status change in the application. WP-053 seeds both
            // confirmed graphs, which is what makes this call safe to activate.
            var transitionResult = await _workflowValidationService.ValidateTransitionAsync(
                WorkflowDomains.Invoice, previousStatus, request.Status, cancellationToken);

            if (transitionResult.IsFailure)
            {
                _logger.LogWarning(
                    "Invoice {InvoiceId} status change {PreviousStatus} -> {NewStatus} rejected: {ErrorCode} - {ErrorMessage}",
                    invoice.Id, previousStatus, request.Status, transitionResult.Error.Code, transitionResult.Error.Message);
                return Result.Failure<InvoiceDto>(transitionResult.Error);
            }

            // (2) Is the acting user allowed to perform THIS PARTICULAR transition?
            // Generalised in WP-053 from WP-051's single hardcoded
            // CHECKED_READY_TO_APPROVE -> APPROVED case to all four gated transitions
            // (see RoleGatedTransitions). Which transitions are gated is a Domain
            // constant; which role each requires comes from the tenant's
            // ApprovalPolicy data (ApprovalDomains.InvoiceApproval), unchanged from
            // WP-051's mechanism.
            if (RoleGatedTransitions.RequiresApprovalRole(previousStatus, request.Status))
            {
                var authorizationResult = await _approvalAuthorizationService.AuthorizeAsync(
                    ApprovalDomains.InvoiceApproval, _currentUserService.Roles, cancellationToken);

                if (authorizationResult.IsFailure)
                {
                    _logger.LogWarning(
                        "Invoice {InvoiceId} transition {PreviousStatus} -> {NewStatus} rejected (role gate): {ErrorCode} - {ErrorMessage}",
                        invoice.Id, previousStatus, request.Status, authorizationResult.Error.Code, authorizationResult.Error.Message);
                    return Result.Failure<InvoiceDto>(authorizationResult.Error);
                }
            }

            // (3) WP-084: every human-initiated status change requires a non-empty
            // note. Checked last, after transition-validity/role-gating, so an
            // attempt that isn't even a valid or authorized transition reports ITS
            // OWN specific error rather than a possibly-misleading "note required"
            // for an action that was never going to succeed anyway - but still
            // before any field is mutated, so the "invoice left completely
            // untouched on rejection" guarantee holds regardless of which check
            // actually failed. The system's own automatic EXTRACTED ->
            // AWAITING_REVIEW advance at ingestion never calls UpdateAsync at all -
            // InvoiceProcessingService constructs the Invoice directly via
            // IInvoiceRepository.AddAsync (confirmed by grepping every call site of
            // UpdateAsync: this HTTP status-transition endpoint is the only one) -
            // so it is structurally unaffected by this check, not specially
            // exempted from it.
            if (string.IsNullOrWhiteSpace(request.Notes))
            {
                return Result.Failure<InvoiceDto>(new Error(
                    "Invoice.NoteRequiredForTransition",
                    "A note is required to change an invoice's status."));
            }

            if (request.Notes.Length > FieldLimits.InvoiceNoteContent)
            {
                return Result.Failure<InvoiceDto>(new Error(
                    "Invoice.InvalidNoteContent",
                    $"Note content must not exceed {FieldLimits.InvoiceNoteContent} characters."));
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

            // WP-084: the note validated as required above is created as part of
            // this same transition - staged here and committed by the single
            // SaveChangesAsync call below, never as an independent follow-up call
            // that could succeed or fail on its own. Mirrors AddNoteAsync's own
            // note-creation + NoteAdded audit staging exactly, just inlined so both
            // the note and the status change share one atomic commit.
            var note = new InvoiceNote
            {
                InvoiceId = invoice.Id,
                Content = request.Notes!,
                AuthorDisplayName = _currentUserService.DisplayName,
            };
            invoice.Notes.Add(note);

            var noteAuditResult = await _auditService.LogAsync(
                new RecordAuditLogRequest(
                    Action: AuditActions.NoteAdded,
                    EntityName: nameof(Invoice),
                    EntityId: invoice.Id,
                    PreviousValue: null,
                    NewValue: request.Notes!),
                cancellationToken);

            if (noteAuditResult.IsFailure)
            {
                _logger.LogWarning(
                    "Failed to stage audit log entry for note added to invoice {InvoiceId} during status change: {ErrorCode} - {ErrorMessage}",
                    invoice.Id, noteAuditResult.Error.Code, noteAuditResult.Error.Message);
            }
        }

        await _invoiceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated invoice {InvoiceId}. Status={Status}.", invoice.Id, invoice.Status);

        // WP-031: the one piece of the query workflow Sprint 1 never built - actually
        // emailing the supplier once a query is raised. Fired only on the specific
        // transition INTO QueryRaised (not, say, re-saving an invoice that is
        // already QueryRaised), using the note this same transition already
        // required (WP-084) as the query content - docs/Sprint2-Plan.md's
        // WP-031/032 scope note confirms no separate "query reason" field exists or
        // is needed. Runs AFTER the status change and note have already committed:
        // a failed or skipped send never unwinds or blocks the transition itself -
        // see IEmailSendService.SendAsync's doc comment for why "log and continue"
        // is the right behavior here, same reasoning as the audit-staging-failure
        // handling above.
        if (statusIsChanging && request.Status == InvoiceStatusCodes.QueryRaised)
        {
            await SendQueryEmailAsync(invoice, request.Notes!, cancellationToken);
        }

        return Result.Success(ToDto(invoice));
    }

    /// <summary>
    /// Sends the outbound query email to the invoice's supplier (WP-031), using the
    /// mandatory transition note as the query content. Never throws and never
    /// returns a failure to its caller - see the call site's comment for why a
    /// failed/skipped send must not affect the already-committed status transition.
    /// </summary>
    private async Task SendQueryEmailAsync(Invoice invoice, string queryNote, CancellationToken cancellationToken)
    {
        var supplierEmail = invoice.Supplier?.Email;
        if (string.IsNullOrWhiteSpace(supplierEmail))
        {
            // WP-031 decision: Supplier.Email is optional (WP-026) - a supplier with
            // no email on file is a normal, expected state, not a data error.
            // docs/AI/06_Domain_Reference_Data.md has no convention covering this
            // case, so the minimal reasoned behavior is used instead: skip the send
            // (the status transition itself has already committed and must not be
            // blocked or unwound by this), log a warning so the gap is visible, and
            // record the open question - should this also surface to a user
            // somewhere in-app, e.g. an IngestionIssue-style flag? - in
            // docs/Backlog.md rather than inventing an answer here.
            _logger.LogWarning(
                "Invoice {InvoiceId} transitioned to {QueryRaisedStatus} but supplier {SupplierId} has no email on file - query email not sent.",
                invoice.Id, InvoiceStatusCodes.QueryRaised, invoice.SupplierId);
            return;
        }

        var subject = $"Query regarding invoice {invoice.SupplierInvoiceNumber ?? invoice.Id.ToString()}";
        var body = BuildQueryEmailBody(invoice, queryNote);

        var sendResult = await _emailSendService.SendAsync(
            new SendEmailRequest(supplierEmail, subject, body, invoice.Supplier?.Name),
            cancellationToken);

        if (sendResult.IsFailure)
        {
            // Same "log loudly, don't fail an already-committed operation" reasoning
            // as the audit-log-staging-failure handling elsewhere in this class.
            _logger.LogWarning(
                "Failed to send query email for invoice {InvoiceId} to {SupplierEmail}: {ErrorCode} - {ErrorMessage}",
                invoice.Id, supplierEmail, sendResult.Error.Code, sendResult.Error.Message);
        }
    }

    private static string BuildQueryEmailBody(Invoice invoice, string queryNote)
    {
        var lines = new[]
        {
            $"A query has been raised regarding invoice {invoice.SupplierInvoiceNumber ?? invoice.Id.ToString()}.",
            string.Empty,
            queryNote,
            string.Empty,
            "Please respond to this email with any information that can help resolve the query.",
        };

        return string.Join(Environment.NewLine, lines);
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
    public async Task<Result<IReadOnlyList<InvoiceNoteDto>>> GetNotesAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _invoiceRepository.GetByIdWithNotesAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure<IReadOnlyList<InvoiceNoteDto>>(new Error("Invoice.NotFound", $"Invoice '{invoiceId}' was not found."));
        }

        return Result.Success<IReadOnlyList<InvoiceNoteDto>>(
            invoice.Notes.OrderByDescending(n => n.CreatedAtUtc).Select(ToNoteDto).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<InvoiceNoteDto>> AddNoteAsync(Guid invoiceId, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Result.Failure<InvoiceNoteDto>(new Error("Invoice.InvalidNoteContent", "Note content must not be empty."));
        }

        if (content.Length > FieldLimits.InvoiceNoteContent)
        {
            return Result.Failure<InvoiceNoteDto>(new Error(
                "Invoice.InvalidNoteContent",
                $"Note content must not exceed {FieldLimits.InvoiceNoteContent} characters."));
        }

        var invoice = await _invoiceRepository.GetByIdWithNotesAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure<InvoiceNoteDto>(new Error("Invoice.NotFound", $"Invoice '{invoiceId}' was not found."));
        }

        // WP-055: AuthorDisplayName is captured once, here, at creation - not
        // recomputed on every read - so it reflects who wrote the note at the
        // time they wrote it. See InvoiceNote.AuthorDisplayName's own doc comment.
        var note = new InvoiceNote
        {
            InvoiceId = invoiceId,
            Content = content,
            AuthorDisplayName = _currentUserService.DisplayName,
        };
        invoice.Notes.Add(note);

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

        // CreatedAtUtc is only populated once SaveChangesAsync has actually run
        // (AuditEntity's own doc comment) - read it AFTER the save above, not
        // before, so the returned DTO carries the real, server-stamped timestamp
        // rather than its unset default.
        return Result.Success(ToNoteDto(note));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<InvoiceExtractedFieldDto>>> GetExtractedFieldsAsync(
        Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var invoice = await _invoiceRepository.GetByIdWithExtractedFieldsAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure<IReadOnlyList<InvoiceExtractedFieldDto>>(new Error("Invoice.NotFound", $"Invoice '{invoiceId}' was not found."));
        }

        // Canonical order, not row/insertion order - EF Core/SQL do not
        // otherwise guarantee an order for these rows (see
        // InvoiceExtractedFieldKeys.CanonicalOrder's own doc comment).
        return Result.Success<IReadOnlyList<InvoiceExtractedFieldDto>>(
            invoice.ExtractedFields
                .OrderBy(f => Array.IndexOf(InvoiceExtractedFieldKeys.CanonicalOrder, f.FieldKey))
                .Select(ToExtractedFieldDto)
                .ToList());
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
        DuplicateMatchInvoiceId: invoice.DuplicateMatchInvoiceId,
        CreatedAtUtc: invoice.CreatedAtUtc);

    private static InvoiceNoteDto ToNoteDto(InvoiceNote note) => new(
        Id: note.Id,
        Content: note.Content,
        AuthorDisplayName: note.AuthorDisplayName,
        CreatedAtUtc: note.CreatedAtUtc);

    private static InvoiceExtractedFieldDto ToExtractedFieldDto(InvoiceExtractedField field) => new(
        FieldKey: field.FieldKey,
        Label: field.Label,
        Value: field.Value,
        ConfidenceScore: field.ConfidenceScore);
}
