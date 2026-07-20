using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace APFlow.Application.Features.Invoices;

/// <summary>
/// Default implementation of <see cref="IInvoiceProcessingService"/>. Depends only
/// on other Application-layer interfaces (never on Infrastructure/Integrations
/// types directly), so this class is fully unit-testable with hand-written fakes -
/// no database, no Graph/Blob/Document Intelligence SDK required. See
/// docs/WP-012-Invoice-Processing-Pipeline-Decisions.md for the reasoning behind
/// the choices this class makes beyond simply chaining its six collaborators.
/// Depends on both <see cref="IInvoiceService"/> (create/advance-status) and
/// <see cref="IInvoiceRepository"/> (persisting a duplicate-check result directly)
/// deliberately - see <see cref="PersistDuplicateCheckResultAsync"/> and
/// docs/WP-010-Duplicate-Flag-Persistence-Decision.md's ruling that
/// <c>DuplicateDetectionService</c> itself must stay a pure compute service with no
/// <c>SaveChangesAsync</c> access, so this orchestrator owns that write instead.
/// </summary>
public sealed class InvoiceProcessingService : IInvoiceProcessingService
{
    // Applied only to the external, read-or-overwrite (and therefore safely
    // repeatable) integration calls - email sync, PDF extraction, blob upload,
    // Document Intelligence analysis, and marking an email processed (documented as
    // idempotent by IEmailSyncService itself). Deliberately NOT applied to Database
    // Save or the duplicate-detection read: both go through AppDbContext, whose
    // SqlServer options already configure EnableRetryOnFailure (see
    // APFlow.Infrastructure.DependencyInjection.AddDatabase) - a second, uncoordinated
    // retry loop here would risk conflicting with that documented execution
    // strategy rather than adding real resilience.
    private const int MaxRetryAttempts = 3;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(500);

    private readonly IEmailSyncService _emailSyncService;
    private readonly IPdfExtractionService _pdfExtractionService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IDocumentAnalysisService _documentAnalysisService;
    private readonly IDuplicateDetectionService _duplicateDetectionService;
    private readonly IInvoiceService _invoiceService;
    private readonly ISupplierService _supplierService;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ILogger<InvoiceProcessingService> _logger;

    /// <summary>Creates a new <see cref="InvoiceProcessingService"/>.</summary>
    public InvoiceProcessingService(
        IEmailSyncService emailSyncService,
        IPdfExtractionService pdfExtractionService,
        IBlobStorageService blobStorageService,
        IDocumentAnalysisService documentAnalysisService,
        IDuplicateDetectionService duplicateDetectionService,
        IInvoiceService invoiceService,
        ISupplierService supplierService,
        IInvoiceRepository invoiceRepository,
        ILogger<InvoiceProcessingService> logger)
    {
        _emailSyncService = emailSyncService;
        _pdfExtractionService = pdfExtractionService;
        _blobStorageService = blobStorageService;
        _documentAnalysisService = documentAnalysisService;
        _duplicateDetectionService = duplicateDetectionService;
        _invoiceService = invoiceService;
        _supplierService = supplierService;
        _invoiceRepository = invoiceRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<InvoiceProcessingResult>> ProcessUnreadEmailsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Invoice processing pipeline run starting.");

        var syncResult = await ExecuteWithRetryAsync(
            () => _emailSyncService.SyncUnreadEmailsAsync(cancellationToken), "SyncUnreadEmails", cancellationToken);

        if (syncResult.IsFailure)
        {
            _logger.LogError(
                "Invoice processing pipeline run aborted: email sync failed after {Attempts} attempt(s). {ErrorCode} - {ErrorMessage}",
                MaxRetryAttempts, syncResult.Error.Code, syncResult.Error.Message);
            return Result.Failure<InvoiceProcessingResult>(syncResult.Error);
        }

        var emails = syncResult.Value;
        _logger.LogInformation("Synced {EmailCount} unread email(s).", emails.Count);

        var items = new List<InvoiceProcessingItemResult>();
        var emailsMarkedProcessed = 0;

        foreach (var email in emails)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var readyToMark = await ProcessEmailAsync(email, items, cancellationToken);
            if (!readyToMark)
            {
                continue;
            }

            var markResult = await ExecuteWithRetryAsync(
                () => _emailSyncService.MarkAsProcessedAsync(email.MessageId, cancellationToken), "MarkAsProcessed", cancellationToken);

            if (markResult.IsSuccess)
            {
                emailsMarkedProcessed++;
            }
            else
            {
                _logger.LogWarning(
                    "Email {MessageId} was fully processed but could not be marked as processed: {ErrorCode} - {ErrorMessage}. " +
                    "It will be re-synced next run; already-saved invoices are safely skipped via idempotency, not duplicated.",
                    email.MessageId, markResult.Error.Code, markResult.Error.Message);
            }
        }

        _logger.LogInformation(
            "Invoice processing pipeline run complete. {EmailCount} email(s) synced, {MarkedCount} marked processed, {ItemCount} attachment(s) handled.",
            emails.Count, emailsMarkedProcessed, items.Count);

        return Result.Success(new InvoiceProcessingResult(emails.Count, emailsMarkedProcessed, items));
    }

    /// <summary>
    /// Handles one synced email end to end and reports whether it is safe to mark
    /// as processed: true if every attachment either processed successfully or was
    /// already processed by a prior run (including the "no PDF attachments" case -
    /// nothing left to do); false if any attachment failed, so the email is left
    /// unread for the next run to retry.
    /// </summary>
    private async Task<bool> ProcessEmailAsync(EmailSummaryDto email, List<InvoiceProcessingItemResult> items, CancellationToken cancellationToken)
    {
        var extractResult = await ExecuteWithRetryAsync(
            () => _pdfExtractionService.ExtractPdfAttachmentsAsync(email.MessageId, cancellationToken), "ExtractPdfAttachments", cancellationToken);

        if (extractResult.IsFailure)
        {
            _logger.LogWarning(
                "PDF extraction failed for email {MessageId} after {Attempts} attempt(s): {ErrorCode} - {ErrorMessage}",
                email.MessageId, MaxRetryAttempts, extractResult.Error.Code, extractResult.Error.Message);
            items.Add(new InvoiceProcessingItemResult(
                email.MessageId, FileName: null, InvoiceProcessingOutcome.Failed, InvoiceId: null, IsPotentialDuplicate: null,
                extractResult.Error.Code, extractResult.Error.Message));
            return false;
        }

        var attachments = extractResult.Value;
        if (attachments.Count == 0)
        {
            _logger.LogInformation("Email {MessageId} had no PDF attachments; nothing to process.", email.MessageId);
            return true;
        }

        var anyFailed = false;

        foreach (var attachment in attachments)
        {
            var outcome = await ProcessAttachmentAsync(email, attachment, cancellationToken);
            items.Add(outcome);

            if (outcome.Outcome == InvoiceProcessingOutcome.Failed)
            {
                anyFailed = true;
            }
        }

        return !anyFailed;
    }

    /// <summary>
    /// Handles one PDF attachment: idempotency check, Blob Storage upload, Document
    /// Intelligence analysis, supplier resolution, database save, and duplicate
    /// detection - in that order. See
    /// docs/WP-012-Invoice-Processing-Pipeline-Decisions.md for why this specific
    /// order, and for how each failure mode is handled.
    /// </summary>
    private async Task<InvoiceProcessingItemResult> ProcessAttachmentAsync(
        EmailSummaryDto email, PdfAttachmentDto attachment, CancellationToken cancellationToken)
    {
        var blobName = BuildBlobName(email.MessageId, attachment.FileName);

        // Idempotency check. Fails CLOSED: if this check itself cannot be completed,
        // this attachment is reported as a failure (not silently reprocessed) rather
        // than risking a duplicate invoice row if the check's own failure is masking
        // a row that genuinely already exists. The email is left unmarked, so this
        // is retried - safely - next run.
        var existingInvoicesResult = await _invoiceService.GetAllAsync(cancellationToken);
        if (existingInvoicesResult.IsFailure)
        {
            return Failed(email.MessageId, attachment.FileName, existingInvoicesResult.Error);
        }

        var existing = existingInvoicesResult.Value.FirstOrDefault(i => i.SourceDocumentBlobName == blobName);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Attachment {FileName} on email {MessageId} was already processed as invoice {InvoiceId}; skipping.",
                attachment.FileName, email.MessageId, existing.Id);
            return new InvoiceProcessingItemResult(
                email.MessageId, attachment.FileName, InvoiceProcessingOutcome.AlreadyProcessed, existing.Id, IsPotentialDuplicate: null, null, null);
        }

        var uploadResult = await ExecuteWithRetryAsync(
            () => _blobStorageService.UploadAsync(blobName, new MemoryStream(attachment.Content), attachment.ContentType, cancellationToken),
            "BlobUpload", cancellationToken);
        if (uploadResult.IsFailure)
        {
            return Failed(email.MessageId, attachment.FileName, uploadResult.Error);
        }

        var analysisResult = await ExecuteWithRetryAsync(
            () => _documentAnalysisService.AnalyzeInvoiceAsync(attachment.Content, cancellationToken), "DocumentAnalysis", cancellationToken);
        if (analysisResult.IsFailure)
        {
            return Failed(email.MessageId, attachment.FileName, analysisResult.Error);
        }

        var extraction = analysisResult.Value;

        var supplierIdResult = await ResolveSupplierAsync(extraction.SupplierName.Value, cancellationToken);
        if (supplierIdResult.IsFailure)
        {
            return Failed(email.MessageId, attachment.FileName, supplierIdResult.Error);
        }

        var createResult = await _invoiceService.CreateAsync(
            new CreateInvoiceRequest(
                SupplierId: supplierIdResult.Value,
                SupplierInvoiceNumber: extraction.SupplierInvoiceNumber.Value,
                InvoiceDate: extraction.InvoiceDate.Value,
                DueDate: extraction.DueDate.Value,
                Currency: extraction.Currency,
                NetAmount: extraction.NetAmount.Value,
                Vat: extraction.Vat.Value,
                GrossTotal: extraction.GrossTotal.Value,
                SourceEmailMessageId: email.MessageId,
                SourceDocumentBlobName: blobName),
            cancellationToken);

        if (createResult.IsFailure)
        {
            return Failed(email.MessageId, attachment.FileName, createResult.Error);
        }

        // CreateAsync always starts a new invoice at Received (see CreateInvoiceRequest's
        // doc comment) - a stable invariant kept for other, non-pipeline callers (e.g. a
        // future manual-entry path). By this point in the pipeline, PDF extraction and
        // Document Intelligence analysis are both already complete, which is exactly what
        // InvoiceStatus.Extracted documents ("PDF attachment extracted and analyzed;
        // structured data is available") - so this pipeline advances the status itself,
        // via the same Update path any other caller would use, rather than special-casing
        // CreateAsync's default for this one caller.
        var invoice = createResult.Value;
        var advanceStatusResult = await _invoiceService.UpdateAsync(
            invoice.Id,
            new UpdateInvoiceRequest(
                invoice.SupplierInvoiceNumber,
                invoice.InvoiceDate,
                invoice.DueDate,
                invoice.Currency,
                invoice.NetAmount,
                invoice.Vat,
                invoice.GrossTotal,
                InvoiceStatus.Extracted),
            cancellationToken);

        if (advanceStatusResult.IsSuccess)
        {
            invoice = advanceStatusResult.Value;
        }
        else
        {
            _logger.LogWarning(
                "Invoice {InvoiceId} was saved but its status could not be advanced to Extracted: {ErrorCode} - {ErrorMessage}. It remains Received.",
                invoice.Id, advanceStatusResult.Error.Code, advanceStatusResult.Error.Message);
        }

        _logger.LogInformation(
            "Saved invoice {InvoiceId} (status {Status}) from email {MessageId}, attachment {FileName}.",
            invoice.Id, invoice.Status, email.MessageId, attachment.FileName);

        // A failed duplicate check does not fail this item - the invoice was already
        // saved successfully. Duplicate detection remains advisory-only (WP-010): it
        // never blocks or auto-rejects an invoice. Per WP-010's persistence ruling
        // (docs/WP-010-Duplicate-Flag-Persistence-Decision.md), a successful check's
        // result is now persisted directly onto the invoice - see
        // PersistDuplicateCheckResultAsync - rather than staying ephemeral.
        bool? isPotentialDuplicate = null;
        var duplicateResult = await _duplicateDetectionService.CheckAsync(invoice.Id, cancellationToken);
        if (duplicateResult.IsSuccess)
        {
            isPotentialDuplicate = duplicateResult.Value.IsPotentialDuplicate;
            if (isPotentialDuplicate == true)
            {
                _logger.LogWarning(
                    "Invoice {InvoiceId} flagged as a potential duplicate of {MatchCount} other invoice(s).",
                    invoice.Id, duplicateResult.Value.Matches.Count);
            }

            await PersistDuplicateCheckResultAsync(invoice.Id, duplicateResult.Value, cancellationToken);
        }
        else
        {
            _logger.LogWarning(
                "Duplicate check failed for invoice {InvoiceId}: {ErrorCode} - {ErrorMessage}. The invoice was still " +
                "saved successfully; its persisted duplicate flag is left at its prior value (false/null for a newly " +
                "created invoice, since there is no successful check result to persist).",
                invoice.Id, duplicateResult.Error.Code, duplicateResult.Error.Message);
        }

        return new InvoiceProcessingItemResult(
            email.MessageId, attachment.FileName, InvoiceProcessingOutcome.Processed, invoice.Id, isPotentialDuplicate, null, null);
    }

    /// <summary>
    /// Persists a successful duplicate check's result directly onto the invoice, via
    /// <see cref="IInvoiceRepository"/> rather than <see cref="IInvoiceService"/> -
    /// per WP-010's ruling that <c>DuplicateDetectionService</c> stays a pure compute
    /// service with no <c>SaveChangesAsync</c> access, so this orchestrator owns the
    /// write instead. A missing invoice here (its id was used to save it moments
    /// earlier in the same method) is logged and skipped rather than treated as
    /// fatal - the invoice itself is already safely saved regardless of whether this
    /// advisory flag can be attached to it. Consistent with every other repository
    /// call in this codebase (e.g. <c>InvoiceService.CreateAsync</c>/<c>UpdateAsync</c>'s
    /// own repository calls), an actual exception here (e.g. a database outage) is
    /// deliberately not caught and propagates rather than being silently swallowed.
    /// </summary>
    private async Task PersistDuplicateCheckResultAsync(
        Guid invoiceId, DuplicateCheckResult duplicateCheckResult, CancellationToken cancellationToken)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            _logger.LogWarning(
                "Could not persist duplicate-check result for invoice {InvoiceId}: invoice not found.", invoiceId);
            return;
        }

        invoice.IsPotentialDuplicate = duplicateCheckResult.IsPotentialDuplicate;
        invoice.DuplicateCheckReason = BuildDuplicateCheckReason(duplicateCheckResult);

        _invoiceRepository.Update(invoice);
        await _invoiceRepository.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Condenses every <see cref="DuplicateMatch.Reason"/> into the single free-text
    /// <see cref="APFlow.Domain.Entities.Invoice.DuplicateCheckReason"/> this pipeline
    /// persists. Null when
    /// there is nothing to explain (not a duplicate).
    /// </summary>
    private static string? BuildDuplicateCheckReason(DuplicateCheckResult result) =>
        result.IsPotentialDuplicate
            ? string.Join(" ", result.Matches.Select(m => m.Reason))
            : null;

    /// <summary>
    /// Resolves the supplier a new invoice should be attached to from its extracted
    /// name: a case-insensitive, trimmed exact match against existing suppliers, or
    /// a newly created supplier if none matches. See
    /// docs/WP-012-Invoice-Processing-Pipeline-Decisions.md for why this matching
    /// strategy was chosen and its known limitations, and for why a null/blank
    /// extracted name is treated as a failure rather than a fabricated placeholder
    /// supplier.
    /// </summary>
    private async Task<Result<Guid>> ResolveSupplierAsync(string? extractedSupplierName, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(extractedSupplierName))
        {
            return Result.Failure<Guid>(new Error(
                "InvoiceProcessing.SupplierNameNotExtracted",
                "Document Intelligence did not extract a supplier name; cannot resolve or create a supplier for this invoice."));
        }

        var trimmedName = extractedSupplierName.Trim();

        var existingSuppliersResult = await _supplierService.GetAllAsync(cancellationToken);
        if (existingSuppliersResult.IsSuccess)
        {
            var match = existingSuppliersResult.Value.FirstOrDefault(
                s => string.Equals(s.Name, trimmedName, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return Result.Success(match.Id);
            }
        }

        var createResult = await _supplierService.CreateAsync(new SaveSupplierRequest(trimmedName), cancellationToken);
        if (createResult.IsFailure)
        {
            return Result.Failure<Guid>(createResult.Error);
        }

        _logger.LogInformation(
            "Created new supplier {SupplierId} ({SupplierName}) during invoice processing.",
            createResult.Value.Id, createResult.Value.Name);
        return Result.Success(createResult.Value.Id);
    }

    private static InvoiceProcessingItemResult Failed(string messageId, string fileName, Error error) =>
        new(messageId, fileName, InvoiceProcessingOutcome.Failed, InvoiceId: null, IsPotentialDuplicate: null, error.Code, error.Message);

    /// <summary>
    /// Builds the deterministic logical blob name for a given email+attachment
    /// combination - both where the PDF is stored and, via
    /// <see cref="APFlow.Domain.Entities.Invoice.SourceDocumentBlobName"/>, this
    /// pipeline's idempotency key.
    /// </summary>
    private static string BuildBlobName(string messageId, string fileName) => $"invoices/{messageId}/{fileName}";

    private async Task<Result<T>> ExecuteWithRetryAsync<T>(
        Func<Task<Result<T>>> operation, string operationName, CancellationToken cancellationToken)
    {
        var lastError = Error.None;

        for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            var result = await operation();
            if (result.IsSuccess)
            {
                if (attempt > 1)
                {
                    _logger.LogInformation("{Operation} succeeded on attempt {Attempt}.", operationName, attempt);
                }

                return result;
            }

            lastError = result.Error;
            _logger.LogWarning(
                "{Operation} failed on attempt {Attempt}/{MaxAttempts}: {ErrorCode} - {ErrorMessage}",
                operationName, attempt, MaxRetryAttempts, result.Error.Code, result.Error.Message);

            if (attempt < MaxRetryAttempts)
            {
                await Task.Delay(RetryDelay * attempt, cancellationToken);
            }
        }

        return Result.Failure<T>(lastError);
    }

    private async Task<Result> ExecuteWithRetryAsync(
        Func<Task<Result>> operation, string operationName, CancellationToken cancellationToken)
    {
        var lastError = Error.None;

        for (var attempt = 1; attempt <= MaxRetryAttempts; attempt++)
        {
            var result = await operation();
            if (result.IsSuccess)
            {
                if (attempt > 1)
                {
                    _logger.LogInformation("{Operation} succeeded on attempt {Attempt}.", operationName, attempt);
                }

                return result;
            }

            lastError = result.Error;
            _logger.LogWarning(
                "{Operation} failed on attempt {Attempt}/{MaxAttempts}: {ErrorCode} - {ErrorMessage}",
                operationName, attempt, MaxRetryAttempts, result.Error.Code, result.Error.Message);

            if (attempt < MaxRetryAttempts)
            {
                await Task.Delay(RetryDelay * attempt, cancellationToken);
            }
        }

        return Result.Failure(lastError);
    }
}
