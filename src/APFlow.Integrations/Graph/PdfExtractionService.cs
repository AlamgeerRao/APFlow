using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APFlow.Integrations.Graph;

/// <summary>
/// Graph-backed implementation of <see cref="IPdfExtractionService"/>. Depends on
/// <see cref="IGraphAttachmentOperations"/> rather than the Graph SDK directly - see
/// that interface's doc comment for why. All the filtering logic worth testing
/// (PDF detection, signature verification, inline skip, unsupported-type skip) lives
/// here.
/// SECURITY: attachments arrive as untrusted external input (vendor/supplier email).
/// Content-type and file extension are both fully controlled by the sender and are
/// only used as a first-pass filter - see <see cref="HasPdfSignature"/> for the
/// defense-in-depth check against mislabeled content.
/// KNOWN, ACCEPTED LIMITATION: extracted bytes are fully materialized into memory
/// with no size cap. Low risk for the current single-message, synchronous usage;
/// worth a configurable max-size guard if this is ever called in a batch/loop over
/// many messages by a future Workers job.
/// </summary>
public sealed class PdfExtractionService : IPdfExtractionService
{
    private const string PdfContentType = "application/pdf";
    private const string PdfExtension = ".pdf";

    // The PDF file signature (magic bytes) - "%PDF-" as ASCII. Verified against the
    // actual byte content as defense-in-depth: ContentType and file extension are
    // both attacker-controlled (external email input), so a file merely claiming to
    // be a PDF is not trusted as one until its content is verified.
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();

    private readonly IGraphAttachmentOperations _attachmentOperations;
    private readonly GraphOptions _options;
    private readonly ILogger<PdfExtractionService> _logger;

    /// <summary>Creates a new <see cref="PdfExtractionService"/>.</summary>
    internal PdfExtractionService(IGraphAttachmentOperations attachmentOperations, IOptions<GraphOptions> options, ILogger<PdfExtractionService> logger)
    {
        _attachmentOperations = attachmentOperations;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<PdfAttachmentDto>>> ExtractPdfAttachmentsAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return Result.Failure<IReadOnlyList<PdfAttachmentDto>>(
                new Error("PdfExtraction.InvalidMessageId", "Message id must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(_options.MailboxUserPrincipalName))
        {
            _logger.LogWarning("Graph:MailboxUserPrincipalName is not configured; cannot extract attachments.");
            return Result.Failure<IReadOnlyList<PdfAttachmentDto>>(
                new Error("PdfExtraction.MailboxNotConfigured", "Graph:MailboxUserPrincipalName is not configured."));
        }

        try
        {
            var attachments = await _attachmentOperations.GetAttachmentsAsync(_options.MailboxUserPrincipalName, messageId, cancellationToken);

            var extracted = new List<PdfAttachmentDto>();
            var skippedInlineCount = 0;
            var skippedUnsupportedCount = 0;

            foreach (var attachment in attachments)
            {
                if (attachment.IsInline)
                {
                    skippedInlineCount++;
                    _logger.LogInformation(
                        "Skipped inline attachment '{FileName}' for message {MessageId}.",
                        attachment.FileName, messageId);
                    continue;
                }

                if (!IsPdf(attachment))
                {
                    skippedUnsupportedCount++;
                    _logger.LogInformation(
                        "Skipped unsupported attachment '{FileName}' ({ContentType}) for message {MessageId}.",
                        attachment.FileName, attachment.ContentType, messageId);
                    continue;
                }

                if (!attachment.IsFileAttachment || attachment.Content is null)
                {
                    // Looks like a PDF by name/content-type but isn't a real file
                    // attachment (e.g. a reference/cloud-link attachment) - no bytes
                    // to extract. Treated as unsupported, not as an error.
                    skippedUnsupportedCount++;
                    _logger.LogWarning(
                        "Attachment '{FileName}' for message {MessageId} looked like a PDF but has no extractable content (not a file attachment).",
                        attachment.FileName, messageId);
                    continue;
                }

                if (!HasPdfSignature(attachment.Content))
                {
                    // Content-type/filename claimed this was a PDF, but the actual
                    // bytes don't start with the PDF signature - both of those claims
                    // are sender-controlled and untrusted. Skipped as a distinct,
                    // more specific case than a generic unsupported type, since a
                    // mismatch here is more suspicious than a file that was never
                    // claimed to be a PDF in the first place (could be mislabeled,
                    // corrupted, or deliberately crafted to look like a PDF to a
                    // downstream consumer).
                    skippedUnsupportedCount++;
                    _logger.LogWarning(
                        "Attachment '{FileName}' for message {MessageId} was labeled as a PDF (content-type/extension) " +
                        "but its content does not start with the PDF file signature - skipped as a defense-in-depth measure.",
                        attachment.FileName, messageId);
                    continue;
                }

                extracted.Add(new PdfAttachmentDto(attachment.FileName, attachment.SizeInBytes, attachment.ContentType, attachment.Content));
            }

            _logger.LogInformation(
                "Attachment extraction completed for message {MessageId}: {ExtractedCount} PDF(s) extracted, " +
                "{SkippedInlineCount} inline skipped, {SkippedUnsupportedCount} unsupported skipped.",
                messageId, extracted.Count, skippedInlineCount, skippedUnsupportedCount);

            return Result.Success<IReadOnlyList<PdfAttachmentDto>>(extracted);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Handled gracefully, same pattern as EmailSyncService (WP-006): logged,
            // not rethrown - a Graph outage during extraction should surface as a
            // failed Result the caller can act on, not crash whatever called this.
            _logger.LogError(ex, "Attachment extraction failed for message {MessageId}.", messageId);
            return Result.Failure<IReadOnlyList<PdfAttachmentDto>>(
                new Error("PdfExtraction.ExtractionFailed", $"Failed to extract attachments for message '{messageId}'."));
        }
    }

    /// <summary>
    /// A PDF is anything Graph reports as content-type "application/pdf", OR whose
    /// file name ends in ".pdf" - the OR is deliberate: some senders/mail clients
    /// send PDFs with a generic content-type (e.g. "application/octet-stream") but a
    /// correct file extension. No magic-byte/file-signature sniffing is done - that
    /// would be meaningful extra work for a case not evidenced as a real problem yet;
    /// add it if a real PDF is ever missed because of this.
    /// </summary>
    private static bool IsPdf(GraphAttachmentInfo attachment)
    {
        var contentTypeIsPdf = string.Equals(attachment.ContentType, PdfContentType, StringComparison.OrdinalIgnoreCase);
        var fileNameLooksLikePdf = attachment.FileName.EndsWith(PdfExtension, StringComparison.OrdinalIgnoreCase);
        return contentTypeIsPdf || fileNameLooksLikePdf;
    }

    /// <summary>
    /// Verifies the actual byte content starts with the PDF file signature
    /// ("%PDF-"). Defense-in-depth against mislabeled or malicious content claiming
    /// to be a PDF via content-type/filename alone - see the class-level SECURITY note.
    /// </summary>
    private static bool HasPdfSignature(byte[] content)
    {
        if (content.Length < PdfSignature.Length)
        {
            return false;
        }

        return content.AsSpan(0, PdfSignature.Length).SequenceEqual(PdfSignature);
    }
}
