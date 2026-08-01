namespace APFlow.Application.DTOs;

/// <summary>
/// Read shape for an <see cref="APFlow.Domain.Entities.IngestionIssue"/> row
/// (WP-076) - the Inbox's "unprocessable email" listing.
/// </summary>
/// <param name="Id">The row's id.</param>
/// <param name="SenderAddress">The sender's email address.</param>
/// <param name="SenderName">The sender's display name, if known.</param>
/// <param name="Subject">The email subject.</param>
/// <param name="FirstSeenUtc">When the first occurrence of this issue was received.</param>
/// <param name="LastSeenUtc">When the most recent occurrence was recorded.</param>
/// <param name="OccurrenceCount">How many times this conversation has produced this issue.</param>
/// <param name="ReasonCode">Why the email could not be processed - see <see cref="APFlow.Domain.Common.Constants.IngestionIssueReasonCodes"/>.</param>
/// <param name="AttachmentsFound">Human-readable summary of the attachment filenames/content-types found on the most recent occurrence.</param>
public sealed record IngestionIssueDto(
    Guid Id,
    string SenderAddress,
    string? SenderName,
    string Subject,
    DateTimeOffset FirstSeenUtc,
    DateTimeOffset LastSeenUtc,
    int OccurrenceCount,
    string ReasonCode,
    string AttachmentsFound);

/// <summary>
/// Paging/sort parameters for <see cref="APFlow.Application.Interfaces.IIngestionIssueQueryService"/> /
/// <see cref="APFlow.Application.Interfaces.IIngestionIssueRepository.QueryAsync"/>.
/// Mirrors WP-013's <c>AuditLogQueryParameters</c> shape and conventions - no filter
/// fields yet, since WP-076 only asks for a plain paginated list; add filters if a
/// real requirement calls for them.
/// </summary>
/// <param name="Page">1-based page number. Must be 1 or greater.</param>
/// <param name="PageSize">Rows per page. Must be between 1 and <see cref="MaxPageSize"/>.</param>
/// <param name="SortDescending">Sort direction by <see cref="IngestionIssueDto.LastSeenUtc"/>. Defaults to descending (most recently seen first) - the Inbox wants its noisiest/newest issues on top.</param>
public sealed record IngestionIssueQueryParameters(
    int Page = 1,
    int PageSize = 25,
    bool SortDescending = true)
{
    /// <summary>Upper bound on <see cref="PageSize"/>. Enforced by <c>IngestionIssueQueryService</c>, and defensively clamped again in <c>IngestionIssueRepository.QueryAsync</c> - same defense-in-depth pattern as WP-013's <c>AuditLogQueryParameters.MaxPageSize</c>.</summary>
    public const int MaxPageSize = 100;
}
