namespace APFlow.Application.DTOs;

/// <summary>
/// Metadata for a single email retrieved during sync. Deliberately metadata-only -
/// no body content, no attachments. PDF extraction, attachment/Blob Storage handling,
/// AI processing, and invoice parsing are all explicitly out of scope for WP-006 and
/// must not be added to this shape without a real requirement driving it.
/// <see cref="ConversationId"/> was added by WP-076 - a real requirement (dedup key
/// for <c>IngestionIssue</c>), not a speculative addition.
/// </summary>
/// <param name="MessageId">The Graph message id - stable identifier for future operations (e.g. marking as processed).</param>
/// <param name="Subject">The email subject line.</param>
/// <param name="SenderAddress">The sender's email address.</param>
/// <param name="SenderName">The sender's display name, if Graph provided one.</param>
/// <param name="ReceivedAtUtc">When the email was received, per Graph's receivedDateTime.</param>
/// <param name="ConversationId">Graph's conversationId - groups an email with its replies in the same thread (WP-076).</param>
public sealed record EmailSummaryDto(
    string MessageId,
    string Subject,
    string SenderAddress,
    string? SenderName,
    DateTimeOffset ReceivedAtUtc,
    string ConversationId);
