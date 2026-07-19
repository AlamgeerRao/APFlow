using APFlow.Application.DTOs;
using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Extracts PDF attachments from a synchronised email (see
/// APFlow.Integrations.Graph.PdfExtractionService). WP-007 scope: identifies and
/// extracts PDF file attachments only - inline images and other unsupported
/// attachment types are skipped, not extracted. Does not perform OCR, does not
/// upload anything to Blob Storage, does not validate invoice content, and does not
/// detect duplicates - all explicitly out of scope. This service hands raw PDF bytes
/// back to its caller; what the caller does with them is a future work package's
/// concern.
/// </summary>
public interface IPdfExtractionService
{
    /// <summary>
    /// Extracts every PDF file attachment from the given email. Non-PDF attachments,
    /// inline attachments, and non-file attachments (e.g. reference/cloud-link
    /// attachments with no bytes) are silently skipped, not treated as errors - only
    /// a genuine failure to reach Graph or read the message returns a failed
    /// <see cref="Result"/>. An email with zero PDF attachments is a successful
    /// result containing an empty list, not a failure.
    /// </summary>
    Task<Result<IReadOnlyList<PdfAttachmentDto>>> ExtractPdfAttachmentsAsync(string messageId, CancellationToken cancellationToken = default);
}
