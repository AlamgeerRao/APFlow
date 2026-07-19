using APFlow.Application.DTOs;
using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Synchronises emails from the configured Microsoft 365 mailbox (see
/// APFlow.Integrations.Graph.EmailSyncService). WP-006 scope: reads unread email
/// metadata and marks messages as processed via an application category. Does not
/// read email body content, download attachments, or do anything with PDF
/// extraction/AI processing/invoice parsing - all explicitly out of scope.
/// Deliberately not wired into a scheduled/background polling loop by this work
/// package - that is APFlow.Workers' responsibility (see solution structure), and
/// nothing in WP-006's task list asked for a polling/scheduling mechanism, so one
/// was not invented. This interface is what a future Workers job would call.
/// </summary>
public interface IEmailSyncService
{
    /// <summary>
    /// Reads unread email metadata from the configured mailbox. Does not mark
    /// anything as read or processed - that is a separate, explicit step via
    /// <see cref="MarkAsProcessedAsync"/>, so a caller can choose which synced emails
    /// it actually finished processing.
    /// </summary>
    Task<Result<IReadOnlyList<EmailSummaryDto>>> SyncUnreadEmailsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the given message as processed by applying the configured application
    /// category (see GraphOptions.ProcessedCategoryName). Never deletes or moves the
    /// message. Idempotent - marking an already-marked message succeeds without
    /// duplicating the category.
    /// </summary>
    Task<Result> MarkAsProcessedAsync(string messageId, CancellationToken cancellationToken = default);
}
