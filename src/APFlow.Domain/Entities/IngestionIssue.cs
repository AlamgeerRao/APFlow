using APFlow.Domain.Common.Constants;

namespace APFlow.Domain.Entities;

/// <summary>
/// A record of an inbound email the ingestion pipeline could not turn into an
/// invoice (WP-076) - today, exclusively "no processable PDF attachment" (see
/// <see cref="IngestionIssueReasonCodes"/>). Exists so this failure mode is
/// persisted and surfaced (Inbox, future alerting) instead of only ever appearing
/// as an Information-level log line nobody watches - the exact gap WP-076 closes in
/// <c>InvoiceProcessingService.ProcessEmailAsync</c>.
/// Deduplicated per <see cref="ConversationId"/>: a reply-chain that keeps arriving
/// without a usable attachment increments <see cref="OccurrenceCount"/> and bumps
/// <see cref="LastSeenUtc"/> on the same row rather than creating a new one each
/// time, so a noisy thread produces one Inbox entry, not one per message.
/// </summary>
public sealed class IngestionIssue : TenantEntity
{
    /// <summary>
    /// The Graph message id of the occurrence that produced this row. Updated to
    /// the most recent occurrence's message id on every increment - same
    /// traceability reasoning as <see cref="Invoice.SourceEmailMessageId"/>, not a
    /// foreign key, not validated against Graph.
    /// </summary>
    public string MessageId { get; set; } = string.Empty;

    /// <summary>The sender's email address.</summary>
    public string SenderAddress { get; set; } = string.Empty;

    /// <summary>The sender's display name, if known.</summary>
    public string? SenderName { get; set; }

    /// <summary>The email subject.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>When the first occurrence of this issue was received. Does not change on subsequent occurrences - see <see cref="LastSeenUtc"/> for the most recent one.</summary>
    public DateTimeOffset ReceivedAtUtc { get; set; }

    /// <summary>
    /// The Graph conversation id linking this email to any replies in the same
    /// thread - the dedup key: a second reply in the same conversation with no
    /// usable attachment increments this row rather than creating a second one.
    /// </summary>
    public string ConversationId { get; set; } = string.Empty;

    /// <summary>
    /// A human-readable summary of the attachment filenames/content-types found on
    /// the most recent occurrence (e.g. "receipt.png (image/png)"), or "(none)" if
    /// the email had no attachments at all. Free text, not structured data - this
    /// is diagnostic/display information, not something queried on.
    /// </summary>
    public string AttachmentsFound { get; set; } = string.Empty;

    /// <summary>Why this email could not be processed. See <see cref="IngestionIssueReasonCodes"/> - only <see cref="IngestionIssueReasonCodes.NoProcessableAttachments"/> exists today.</summary>
    public string ReasonCode { get; set; } = IngestionIssueReasonCodes.NoProcessableAttachments;

    /// <summary>How many times this same conversation has produced this issue. Starts at 1; incremented (never reset) on each subsequent occurrence.</summary>
    public int OccurrenceCount { get; set; } = 1;

    /// <summary>When the most recent occurrence of this issue was recorded. Equal to <see cref="ReceivedAtUtc"/> for a row with only one occurrence.</summary>
    public DateTimeOffset LastSeenUtc { get; set; }
}
