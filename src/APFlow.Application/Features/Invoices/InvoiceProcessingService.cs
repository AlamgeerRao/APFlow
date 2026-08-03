using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Entities;
using APFlow.Domain.Common.Constants;
using Microsoft.Extensions.Logging;

namespace APFlow.Application.Features.Invoices;

/// <summary>
/// Default implementation of <see cref="IInvoiceProcessingService"/>. Depends only
/// on other Application-layer interfaces (never on Infrastructure/Integrations
/// types directly), so this class is fully unit-testable with hand-written fakes -
/// no database, no Graph/Blob/Document Intelligence SDK required. See
/// docs/WP-012-Invoice-Processing-Pipeline-Decisions.md for the reasoning behind
/// the choices this class makes beyond simply chaining its six collaborators.
/// Depends on both <see cref="IInvoiceService"/> (idempotency lookups only - see
/// <see cref="ProcessAttachmentAsync"/>) and <see cref="IInvoiceRepository"/>
/// (constructing, duplicate-checking, and saving a new invoice as one atomic unit
/// of work) deliberately - see docs/WP-049-Wire-Duplicate-Detection-Into-Pipeline.md:
/// WP-049 requires that an invoice is never visible in a database state where
/// duplicate-checking hasn't yet run, which rules out
/// <see cref="IInvoiceService.CreateAsync"/> (commits independently, before a
/// check could run) and <c>IInvoiceRepository.PersistDuplicateCheckResultAsync</c>
/// (WP-048; designed to update an invoice that already exists - not usable here,
/// since at the point the check needs to run, the invoice does not exist in the
/// database yet). This orchestrator constructs the <see cref="Invoice"/> directly,
/// runs the (WP-048, pure/synchronous) duplicate check against it in memory, sets
/// the result directly on the same not-yet-saved entity, and saves once.
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

    // WP-082: bounds the "no processable attachment" reprocessing loop QA found live
    // (one email reprocessed 719 times over ~12.4 hours - root-caused to a since-fixed
    // isRead-based Graph filter bug, 7bb3e04, that predates this cap). Independent of
    // that specific root cause: nothing before this WP capped how many times a single
    // conversation could be re-extracted and re-recorded if it keeps coming back as
    // "unprocessed" for any reason, including a future, different bug of the same
    // shape. Once OccurrenceCount reaches this, further cycles skip re-extraction and
    // re-recording entirely (see the cap check in ProcessEmailAsync) - but still
    // attempt MarkAsProcessedAsync each cycle, so a transient mark failure can still
    // self-resolve without needing this cap to ever have been hit for a "real",
    // fixable reason.
    private const int MaxIngestionIssueOccurrences = 5;

    private readonly IEmailSyncService _emailSyncService;
    private readonly IPdfExtractionService _pdfExtractionService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IDocumentAnalysisService _documentAnalysisService;
    private readonly IDuplicateDetectionService _duplicateDetectionService;
    private readonly IInvoiceService _invoiceService;
    private readonly ISupplierService _supplierService;
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IIngestionIssueRepository _ingestionIssueRepository;
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
        IIngestionIssueRepository ingestionIssueRepository,
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
        _ingestionIssueRepository = ingestionIssueRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<InvoiceProcessingResult>> ProcessUnreadEmailsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Invoice processing pipeline run starting.");

        var syncResult = await ExecuteWithRetryAsync(
            () => _emailSyncService.SyncUnprocessedEmailsAsync(cancellationToken), "SyncUnprocessedEmails", cancellationToken);

        if (syncResult.IsFailure)
        {
            _logger.LogError(
                "Invoice processing pipeline run aborted: email sync failed after {Attempts} attempt(s). {ErrorCode} - {ErrorMessage}",
                MaxRetryAttempts, syncResult.Error.Code, syncResult.Error.Message);
            return Result.Failure<InvoiceProcessingResult>(syncResult.Error);
        }

        var emails = syncResult.Value;
        _logger.LogInformation("Synced {EmailCount} unprocessed email(s).", emails.Count);

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
    /// unmarked (no processed category applied) for the next run to retry.
    /// </summary>
    private async Task<bool> ProcessEmailAsync(EmailSummaryDto email, List<InvoiceProcessingItemResult> items, CancellationToken cancellationToken)
    {
        // WP-082 cap check, before doing any of this cycle's real work: a
        // conversation that has already hit MaxIngestionIssueOccurrences from prior
        // cycles is skipped here rather than re-extracted and re-recorded again.
        // Looked up once and threaded into RecordIngestionIssueAsync below (for the
        // not-yet-capped path) instead of a second, redundant lookup there.
        var existingIssue = await _ingestionIssueRepository.GetByConversationIdAsync(email.ConversationId, cancellationToken);
        if (existingIssue is not null && existingIssue.OccurrenceCount >= MaxIngestionIssueOccurrences)
        {
            _logger.LogError(
                "Email {MessageId} (conversation {ConversationId}) has reached the {MaxOccurrences}-occurrence " +
                "reprocessing cap (currently at {OccurrenceCount}) and will not be re-extracted or re-recorded this " +
                "cycle. MarkAsProcessed will still be attempted, so this can still resolve on its own if that " +
                "succeeds - see docs/Backlog.md WP-082.",
                email.MessageId, email.ConversationId, MaxIngestionIssueOccurrences, existingIssue.OccurrenceCount);
            return true;
        }

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

        var attachments = extractResult.Value.PdfAttachments;
        if (attachments.Count == 0)
        {
            await RecordIngestionIssueAsync(email, extractResult.Value.AllAttachments, existingIssue, cancellationToken);
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
    /// Persists (or bumps) the <see cref="IngestionIssue"/> for an email that had no
    /// processable PDF attachment (WP-076) - the fix for the silent failure this
    /// branch used to be (an Information-level log line and nothing else).
    /// Deduplicated per <see cref="EmailSummaryDto.ConversationId"/>: a reply-chain
    /// that keeps arriving without a usable attachment increments the same row
    /// rather than creating a new one per message. A failure here is logged and
    /// swallowed, not propagated - same reasoning as
    /// <see cref="ProcessAttachmentAsync"/>'s own database-save try/catch: this is a
    /// diagnostic side record, not the pipeline's core job of deciding whether the
    /// email had anything to process (it didn't, regardless of whether this write
    /// succeeds), so a transient database error here must not leave an otherwise-fine
    /// "nothing to process" email stuck unmarked forever. <c>existing</c> is the same
    /// conversation's row, if any - already looked up by the caller
    /// (<see cref="ProcessEmailAsync"/>'s WP-082 cap check), so this method doesn't
    /// re-query for it.
    /// </summary>
    private async Task RecordIngestionIssueAsync(EmailSummaryDto email, IReadOnlyList<AttachmentInfoDto> allAttachments, IngestionIssue? existing, CancellationToken cancellationToken)
    {
        var attachmentsFound = allAttachments.Count == 0
            ? "(none)"
            : string.Join("; ", allAttachments.Select(a => $"{a.FileName} ({a.ContentType})"));

        try
        {
            var nowUtc = DateTimeOffset.UtcNow;

            if (existing is not null)
            {
                existing.MessageId = email.MessageId;
                existing.AttachmentsFound = attachmentsFound;
                existing.OccurrenceCount++;
                existing.LastSeenUtc = nowUtc;
            }
            else
            {
                await _ingestionIssueRepository.AddAsync(new IngestionIssue
                {
                    MessageId = email.MessageId,
                    SenderAddress = email.SenderAddress,
                    SenderName = email.SenderName,
                    Subject = email.Subject,
                    ReceivedAtUtc = email.ReceivedAtUtc,
                    ConversationId = email.ConversationId,
                    AttachmentsFound = attachmentsFound,
                    ReasonCode = IngestionIssueReasonCodes.NoProcessableAttachments,
                    OccurrenceCount = 1,
                    LastSeenUtc = nowUtc,
                }, cancellationToken);
            }

            await _ingestionIssueRepository.SaveChangesAsync(cancellationToken);

            _logger.LogWarning(
                "Email {MessageId} (conversation {ConversationId}) had no processable PDF attachment. Attachments found: {AttachmentsFound}.",
                email.MessageId, email.ConversationId, attachmentsFound);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to record ingestion issue for email {MessageId} (conversation {ConversationId}).",
                email.MessageId, email.ConversationId);
        }
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

        // WP-052: the idempotency key is the PDF's content hash, not its blob name.
        // The old blob-name-based key (messageId + fileName) had a known,
        // documented gap (docs/WP-012-Invoice-Processing-Pipeline-Decisions.md
        // item 2): two DISTINCT attachments on the same email that happened to
        // share a file name would collide, and the second would be silently
        // skipped as "already processed" - a real data-loss risk, not just a
        // theoretical one. Hashing the actual bytes means only genuinely
        // identical documents collide, regardless of what either is named.
        // Computed here, once, from the byte array WP-007's extraction already
        // produced in memory - no re-read of the attachment.
        var contentHash = ComputeContentHash(attachment.Content);

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

        // WP-080: content hash alone was too broad a key - it was live-test-confirmed
        // to also match a genuinely SEPARATE invoice that happens to have identical
        // attachment bytes (e.g. the same supplier's PDF template resent for a
        // different real invoice, or the same invoice legitimately re-sent as its
        // own new email), silently skipping it instead of letting it flow through
        // to duplicate detection where it belongs. Idempotency's actual job is
        // narrower than that: recognize a RETRY of processing this exact email (a
        // transient failure partway through, then a re-sync of the same still-unread
        // message), not "any invoice anywhere with byte-identical content." Scoping
        // to (SourceEmailMessageId, contentHash) together keeps the original retry
        // guarantee intact while letting a second, different email with identical
        // content proceed to full processing and IDuplicateDetectionService's own
        // Supplier+InvoiceNumber check - the mechanism actually designed to flag
        // that case, rather than a side effect of the idempotency key being too wide.
        var existing = existingInvoicesResult.Value.FirstOrDefault(
            i => i.SourceDocumentContentHash == contentHash && i.SourceEmailMessageId == email.MessageId);
        if (existing is not null)
        {
            _logger.LogInformation(
                "Attachment {FileName} on email {MessageId} was already processed as invoice {InvoiceId} (matched by content hash within the same email).",
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

        var validationError = InvoiceFieldValidation.Validate(extraction.SupplierInvoiceNumber.Value, extraction.Currency);
        if (validationError is not null)
        {
            return Failed(email.MessageId, attachment.FileName, validationError);
        }

        // WP-049: duplicate detection must run, and its result must be persisted,
        // in the SAME unit of work as the invoice's initial save - an invoice must
        // never be visible with a stale/default duplicate flag before a real check
        // has run. This rules out the WP-012/WP-048 approach of creating the
        // invoice (its own commit) and persisting the duplicate-check result
        // separately afterwards (a second commit) - there was always a real, if
        // brief, window where the invoice existed with a default (not yet
        // meaningful) IsPotentialDuplicate=false. Instead, the invoice is
        // constructed and staged here directly via IInvoiceRepository (not
        // IInvoiceService.CreateAsync/UpdateAsync, both of which commit
        // independently) - validated via InvoiceFieldValidation (the same
        // validation IInvoiceService.CreateAsync uses, shared rather than
        // duplicated) - and created already at InvoiceStatusCodes.AwaitingReview
        // (WP-071), not Extracted: PDF extraction and Document Intelligence
        // analysis are both complete by this point (see InvoiceStatusCodes.Extracted's
        // own doc comment for what that status itself means), and EXTRACTED ->
        // AWAITING_REVIEW is a confirmed, already-seeded edge in both templates'
        // graphs (WorkflowTransitionSeedData) that a reviewer would otherwise have
        // had to trigger manually via a pointless first PATCH before they could do
        // anything else with a freshly-ingested invoice. Rather than the earlier
        // two-step "create at Received, then separately advance to Extracted" (or
        // WP-071's own predecessor, "create at Extracted, leave AWAITING_REVIEW to
        // a manual step"): each of those required its own separate commit or a
        // manual action this pipeline can just as easily skip, incompatible with
        // or superfluous to the atomicity already established here.
        // Deliberately NOT going through IWorkflowValidationService.ValidateTransitionAsync
        // or IApprovalAuthorizationService/RoleGatedTransitions (both of which
        // InvoiceService.UpdateAsync uses for a user-initiated PATCH) - this is a
        // system-initiated advance at record-creation time, and EXTRACTED ->
        // AWAITING_REVIEW is not one of the four RoleGatedTransitions pairs
        // regardless. Constructing the Invoice directly via IInvoiceRepository
        // (as this whole method already does, for the atomicity reasons above)
        // means those checks are architecturally bypassed by construction, not by
        // any new code added here - there is nothing to "call and skip".
        // One side effect, called out deliberately rather than left to be
        // discovered: there is no longer a Received -> Extracted (or, as of
        // WP-071, -> AwaitingReview) status-change event for WP-013's audit log to
        // record for newly-ingested invoices, because the invoice is never
        // actually persisted at any earlier status first - it is created directly
        // at its final initial status. WP-013's audit logging is untouched and
        // still fires normally for any LATER status change (e.g. a future
        // approval/rejection step), since those still go through
        // IInvoiceService.UpdateAsync.
        Invoice? savedInvoice = null;
        bool? isPotentialDuplicate = null;
        Error? saveError = null;

        try
        {
            var otherInvoices = await _invoiceRepository.GetAllAsync(cancellationToken);

            var candidate = new Invoice
            {
                SupplierId = supplierIdResult.Value,
                SupplierInvoiceNumber = extraction.SupplierInvoiceNumber.Value,
                InvoiceDate = extraction.InvoiceDate.Value,
                DueDate = extraction.DueDate.Value,
                Currency = extraction.Currency,
                NetAmount = extraction.NetAmount.Value,
                Vat = extraction.Vat.Value,
                GrossTotal = extraction.GrossTotal.Value,
                Status = InvoiceStatusCodes.AwaitingReview,
                SourceEmailMessageId = email.MessageId,
                SourceDocumentBlobName = blobName,
                SourceDocumentContentHash = contentHash,
            };

            // WP-056: persist the per-field confidence scores WP-008's analysis
            // already produced (extraction.*.Confidence) - previously discarded
            // once each field's .Value was copied onto the scalar properties
            // above. Added to the same not-yet-saved candidate's collection
            // navigation, so these rows commit atomically with the invoice's own
            // insert via the single SaveChangesAsync call below - same reasoning
            // as the duplicate-check fields just above.
            foreach (var field in BuildExtractedFields(candidate.Id, extraction))
            {
                candidate.ExtractedFields.Add(field);
            }

            var duplicateCheckResult = _duplicateDetectionService.Check(candidate, otherInvoices);
            candidate.IsPotentialDuplicate = duplicateCheckResult.IsPotentialDuplicate;
            candidate.DuplicateCheckReason = BuildDuplicateCheckReason(duplicateCheckResult);
            // WP-073: only the first match, same reasoning as Invoice.DuplicateMatchInvoiceId's
            // own doc comment - one navigable reference is what the UI needs.
            candidate.DuplicateMatchInvoiceId = duplicateCheckResult.Matches.FirstOrDefault()?.MatchedInvoiceId;

            await _invoiceRepository.AddAsync(candidate, cancellationToken);
            await _invoiceRepository.SaveChangesAsync(cancellationToken);

            savedInvoice = candidate;
            isPotentialDuplicate = duplicateCheckResult.IsPotentialDuplicate;

            if (isPotentialDuplicate == true)
            {
                _logger.LogWarning(
                    "Invoice {InvoiceId} flagged as a potential duplicate of {MatchCount} other invoice(s) at ingestion.",
                    candidate.Id, duplicateCheckResult.Matches.Count);
            }
        }
        catch (Exception ex)
        {
            // WP-049 task 3: a failure in this step (fetching the comparison set, or
            // the atomic save itself - e.g. a transient database error) must not
            // fail the whole ingestion batch. This is the one place in this method
            // that catches a general exception rather than checking a Result:
            // IDuplicateDetectionService.Check cannot fail (WP-048 made it a pure,
            // non-throwing compute function), and IInvoiceRepository's methods
            // propagate exceptions rather than returning a Result (the established
            // convention elsewhere in this file - see the retry-policy comment
            // above on AppDbContext's EnableRetryOnFailure) - there is no Result to
            // check here, so a try/catch is the only way to isolate this item's
            // failure from the rest of the batch, per this task's explicit
            // requirement. Consistent with every other failure path in this
            // method, the source email is left unmarked, so this attachment is
            // retried on the next run.
            _logger.LogWarning(
                ex,
                "Failed to save invoice with duplicate-check result for email {MessageId}, attachment {FileName}: {ExceptionMessage}",
                email.MessageId, attachment.FileName, ex.Message);
            saveError = new Error("InvoiceProcessing.SaveFailed", ex.Message);
        }

        if (saveError is not null)
        {
            return Failed(email.MessageId, attachment.FileName, saveError);
        }

        _logger.LogInformation(
            "Saved invoice {InvoiceId} (status {Status}) from email {MessageId}, attachment {FileName}. IsPotentialDuplicate={IsPotentialDuplicate}.",
            savedInvoice!.Id, savedInvoice.Status, email.MessageId, attachment.FileName, isPotentialDuplicate);

        return new InvoiceProcessingItemResult(
            email.MessageId, attachment.FileName, InvoiceProcessingOutcome.Processed, savedInvoice.Id, isPotentialDuplicate, null, null);
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
    /// Builds one <see cref="InvoiceExtractedField"/> row per field
    /// <see cref="InvoiceExtractionResult"/> (WP-008) reports (WP-056), including
    /// <see cref="InvoiceExtractionResult.Currency"/> with a null
    /// <see cref="InvoiceExtractedField.ConfidenceScore"/> - per Chief Technical
    /// Architect ruling (2026-07-25): one row per
    /// <see cref="InvoiceExtractionResult"/> field, always, even though
    /// <c>Currency</c> is a plain <c>string</c> never wrapped in
    /// <see cref="ExtractedField{T}"/> (it is reconciled from several monetary
    /// fields rather than separately extracted/confidence-scored - see that
    /// record's own doc comment).
    /// </summary>
    private static List<InvoiceExtractedField> BuildExtractedFields(Guid invoiceId, InvoiceExtractionResult extraction) =>
    [
        NewExtractedField(invoiceId, InvoiceExtractedFieldKeys.SupplierName, "Supplier Name",
            extraction.SupplierName.Value, extraction.SupplierName.Confidence),
        NewExtractedField(invoiceId, InvoiceExtractedFieldKeys.SupplierInvoiceNumber, "Supplier Invoice Number",
            extraction.SupplierInvoiceNumber.Value, extraction.SupplierInvoiceNumber.Confidence),
        NewExtractedField(invoiceId, InvoiceExtractedFieldKeys.InvoiceDate, "Invoice Date",
            FormatDate(extraction.InvoiceDate.Value), extraction.InvoiceDate.Confidence),
        NewExtractedField(invoiceId, InvoiceExtractedFieldKeys.DueDate, "Due Date",
            FormatDate(extraction.DueDate.Value), extraction.DueDate.Confidence),
        NewExtractedField(invoiceId, InvoiceExtractedFieldKeys.NetAmount, "Net Amount",
            FormatDecimal(extraction.NetAmount.Value), extraction.NetAmount.Confidence),
        NewExtractedField(invoiceId, InvoiceExtractedFieldKeys.Vat, "VAT",
            FormatDecimal(extraction.Vat.Value), extraction.Vat.Confidence),
        NewExtractedField(invoiceId, InvoiceExtractedFieldKeys.GrossTotal, "Gross Total",
            FormatDecimal(extraction.GrossTotal.Value), extraction.GrossTotal.Confidence),
        NewExtractedField(invoiceId, InvoiceExtractedFieldKeys.Currency, "Currency",
            extraction.Currency, confidence: null),
    ];

    private static InvoiceExtractedField NewExtractedField(Guid invoiceId, string fieldKey, string label, string? value, double? confidence) =>
        new()
        {
            InvoiceId = invoiceId,
            FieldKey = fieldKey,
            Label = label,
            Value = value,
            ConfidenceScore = confidence,
        };

    // Invariant-culture, unambiguous text representations - this column is
    // display text only (see InvoiceExtractedField.Value's own doc comment),
    // never parsed back, so a fixed, locale-independent format is preferable to
    // whatever the running culture happens to be.
    private static string? FormatDate(DateOnly? value) => value?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static string? FormatDecimal(decimal? value) => value?.ToString(System.Globalization.CultureInfo.InvariantCulture);

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

    /// <summary>
    /// Computes the lowercase-hex SHA-256 hash of a PDF attachment's raw bytes -
    /// WP-052's ingestion idempotency key (see <see cref="Invoice.SourceDocumentContentHash"/>'s
    /// doc comment for why this replaced the blob-name-based key).
    /// </summary>
    private static string ComputeContentHash(byte[] content) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(content));

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
