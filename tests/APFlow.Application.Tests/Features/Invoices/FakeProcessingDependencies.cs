using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;

namespace APFlow.Application.Tests.Features.Invoices;

/// <summary>
/// Hand-written fake, same pattern as every Graph/Blob fake elsewhere in this
/// codebase. Unlike a real mailbox, does NOT remove an email from
/// <see cref="UnreadEmails"/> when <see cref="MarkAsProcessedAsync"/> succeeds -
/// deliberately dumb, so idempotency tests can call
/// <c>ProcessUnreadEmailsAsync</c> twice against unchanged input and prove the
/// pipeline's OWN idempotency check (not this fake's state) is what prevents
/// reprocessing.
/// </summary>
internal sealed class FakeEmailSyncService : IEmailSyncService
{
    public List<EmailSummaryDto> UnreadEmails { get; } = [];
    public List<string> MarkedAsProcessedMessageIds { get; } = [];
    public Result<IReadOnlyList<EmailSummaryDto>>? SyncResultOverride { get; set; }
    public Func<string, Result>? MarkAsProcessedResultFactory { get; set; }

    public Task<Result<IReadOnlyList<EmailSummaryDto>>> SyncUnreadEmailsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(SyncResultOverride ?? Result.Success<IReadOnlyList<EmailSummaryDto>>(UnreadEmails));

    public Task<Result> MarkAsProcessedAsync(string messageId, CancellationToken cancellationToken = default)
    {
        var result = MarkAsProcessedResultFactory?.Invoke(messageId) ?? Result.Success();
        if (result.IsSuccess)
        {
            MarkedAsProcessedMessageIds.Add(messageId);
        }

        return Task.FromResult(result);
    }
}

/// <summary>Hand-written fake, same pattern as every Graph/Blob fake elsewhere in this codebase.</summary>
internal sealed class FakePdfExtractionService : IPdfExtractionService
{
    public Dictionary<string, List<PdfAttachmentDto>> AttachmentsByMessageId { get; } = [];
    public Dictionary<string, Error> FailuresByMessageId { get; } = [];

    public Task<Result<IReadOnlyList<PdfAttachmentDto>>> ExtractPdfAttachmentsAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (FailuresByMessageId.TryGetValue(messageId, out var error))
        {
            return Task.FromResult(Result.Failure<IReadOnlyList<PdfAttachmentDto>>(error));
        }

        var attachments = AttachmentsByMessageId.TryGetValue(messageId, out var list)
            ? (IReadOnlyList<PdfAttachmentDto>)list
            : [];
        return Task.FromResult(Result.Success(attachments));
    }
}

/// <summary>
/// Hand-written fake, same pattern as every Graph/Blob fake elsewhere in this
/// codebase. Only implements the members <c>InvoiceProcessingService</c> actually
/// calls (<c>UploadAsync</c>) - the rest throw, so an accidental call is a loud
/// test failure rather than a silently-wrong default.
/// </summary>
internal sealed class FakeBlobStorageService : IBlobStorageService
{
    public List<string> UploadedBlobNames { get; } = [];
    public Error? UploadFailure { get; set; }
    public Dictionary<string, Error> UploadFailuresByBlobNameContains { get; } = [];

    public Task<Result<string>> UploadAsync(string blobName, Stream content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var matchedFailure = UploadFailuresByBlobNameContains
            .FirstOrDefault(kvp => blobName.Contains(kvp.Key, StringComparison.Ordinal));

        if (matchedFailure.Value is { } specificError)
        {
            return Task.FromResult(Result.Failure<string>(specificError));
        }

        if (UploadFailure is { } error)
        {
            return Task.FromResult(Result.Failure<string>(error));
        }

        UploadedBlobNames.Add(blobName);
        return Task.FromResult(Result.Success($"https://fake.blob.core.windows.net/container/{blobName}"));
    }

    public Task<Result<Stream>> DownloadAsync(string blobName, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("InvoiceProcessingService does not call DownloadAsync.");

    public Task<Result> DeleteAsync(string blobName, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("InvoiceProcessingService does not call DeleteAsync.");

    public Task<Result<Uri>> GenerateSasUrlAsync(string blobName, TimeSpan validFor, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("InvoiceProcessingService does not call GenerateSasUrlAsync.");

    public Task<bool> VerifyContainerAccessAsync(CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("InvoiceProcessingService does not call VerifyContainerAccessAsync.");
}

/// <summary>Hand-written fake, same pattern as every Graph/Blob fake elsewhere in this codebase.</summary>
internal sealed class FakeDocumentAnalysisService : IDocumentAnalysisService
{
    public Result<InvoiceExtractionResult>? ResultOverride { get; set; }
    public InvoiceExtractionResult NextResult { get; set; } = EmptyExtraction();

    public Task<Result<InvoiceExtractionResult>> AnalyzeInvoiceAsync(byte[] pdfContent, CancellationToken cancellationToken = default) =>
        Task.FromResult(ResultOverride ?? Result.Success(NextResult));

    public static InvoiceExtractionResult EmptyExtraction() => new(
        SupplierName: new ExtractedField<string?>(null, null),
        SupplierInvoiceNumber: new ExtractedField<string?>(null, null),
        InvoiceDate: new ExtractedField<DateOnly?>(null, null),
        DueDate: new ExtractedField<DateOnly?>(null, null),
        Currency: null,
        NetAmount: new ExtractedField<decimal?>(null, null),
        Vat: new ExtractedField<decimal?>(null, null),
        GrossTotal: new ExtractedField<decimal?>(null, null));
}

/// <summary>
/// Hand-written fake, same pattern as every Graph/Blob fake elsewhere in this
/// codebase. Defaults to "no duplicates found" so tests that don't care about
/// duplicate detection don't need to configure it explicitly.
/// </summary>
internal sealed class FakeDuplicateDetectionService : IDuplicateDetectionService
{
    public Func<Guid, Result<DuplicateCheckResult>>? ResultFactory { get; set; }

    public Task<Result<DuplicateCheckResult>> CheckAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var result = ResultFactory?.Invoke(invoiceId) ?? Result.Success(new DuplicateCheckResult(invoiceId, false, []));
        return Task.FromResult(result);
    }
}
