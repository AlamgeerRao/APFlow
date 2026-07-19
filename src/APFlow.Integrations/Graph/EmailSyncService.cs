using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APFlow.Integrations.Graph;

/// <summary>
/// Graph-backed implementation of <see cref="IEmailSyncService"/>. Depends on
/// <see cref="IGraphMessageOperations"/> rather than the Graph SDK directly - see
/// that interface's doc comment for why. The category merge/dedup logic in
/// <see cref="MarkAsProcessedAsync"/> is the main thing worth testing in this class,
/// and lives here specifically so it's covered by real unit tests.
/// KNOWN, ACCEPTED RACE: <see cref="MarkAsProcessedAsync"/> reads current categories
/// then does a full-array PATCH - Graph has no atomic "append a category" operation.
/// If another process (a user in Outlook, or a concurrent call) changes categories
/// between the read and the write, that change could be silently overwritten. Low
/// risk for a single AP mailbox with one automated writer; accepted rather than
/// engineered around for WP-006. Revisit if concurrent writers to the same mailbox
/// become a real scenario.
/// </summary>
public sealed class EmailSyncService : IEmailSyncService
{
    private readonly IGraphMessageOperations _messageOperations;
    private readonly GraphOptions _options;
    private readonly ILogger<EmailSyncService> _logger;

    /// <summary>Creates a new <see cref="EmailSyncService"/>.</summary>
    internal EmailSyncService(IGraphMessageOperations messageOperations, IOptions<GraphOptions> options, ILogger<EmailSyncService> logger)
    {
        _messageOperations = messageOperations;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<EmailSummaryDto>>> SyncUnreadEmailsAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.MailboxUserPrincipalName))
        {
            _logger.LogWarning("Graph:MailboxUserPrincipalName is not configured; cannot sync emails.");
            return Result.Failure<IReadOnlyList<EmailSummaryDto>>(
                new Error("EmailSync.MailboxNotConfigured", "Graph:MailboxUserPrincipalName is not configured."));
        }

        _logger.LogInformation("Starting email sync for mailbox {Mailbox}.", _options.MailboxUserPrincipalName);

        try
        {
            var messages = await _messageOperations.GetUnreadMessagesAsync(_options.MailboxUserPrincipalName, cancellationToken);

            _logger.LogInformation(
                "Email sync completed for mailbox {Mailbox}: {Count} unread message(s) found.",
                _options.MailboxUserPrincipalName,
                messages.Count);

            return Result.Success(messages);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Handled gracefully per WP-006 task 7: logged, not rethrown - a Graph
            // outage during sync should not crash whatever called this (e.g. a future
            // Workers polling job), it should be visible as a failed sync in the logs
            // and a Result.Failure the caller can act on (retry later, alert, etc.).
            _logger.LogError(ex, "Email sync failed for mailbox {Mailbox}.", _options.MailboxUserPrincipalName);
            return Result.Failure<IReadOnlyList<EmailSummaryDto>>(
                new Error("EmailSync.SyncFailed", $"Failed to sync emails for mailbox '{_options.MailboxUserPrincipalName}'."));
        }
    }

    /// <inheritdoc />
    public async Task<Result> MarkAsProcessedAsync(string messageId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageId))
        {
            return Result.Failure(new Error("EmailSync.InvalidMessageId", "Message id must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(_options.MailboxUserPrincipalName))
        {
            _logger.LogWarning("Graph:MailboxUserPrincipalName is not configured; cannot mark message as processed.");
            return Result.Failure(new Error("EmailSync.MailboxNotConfigured", "Graph:MailboxUserPrincipalName is not configured."));
        }

        try
        {
            var currentCategories = await _messageOperations.GetMessageCategoriesAsync(_options.MailboxUserPrincipalName, messageId, cancellationToken);

            if (currentCategories.Contains(_options.ProcessedCategoryName, StringComparer.OrdinalIgnoreCase))
            {
                // Idempotent: already marked, nothing to do. Not an error - a caller
                // retrying after a partial failure elsewhere shouldn't get a failure
                // here just because a previous attempt already succeeded.
                _logger.LogInformation("Message {MessageId} is already marked as processed; no change made.", messageId);
                return Result.Success();
            }

            // Preserves every existing category (a user's own Outlook categorization)
            // and only adds the application's category - Graph's PATCH replaces the
            // entire categories array, so this merge has to happen before the call,
            // not rely on the API to append.
            var updatedCategories = currentCategories.Append(_options.ProcessedCategoryName).ToList();

            await _messageOperations.SetMessageCategoriesAsync(_options.MailboxUserPrincipalName, messageId, updatedCategories, cancellationToken);

            _logger.LogInformation("Marked message {MessageId} as processed in mailbox {Mailbox}.", messageId, _options.MailboxUserPrincipalName);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to mark message {MessageId} as processed.", messageId);
            return Result.Failure(new Error("EmailSync.MarkProcessedFailed", $"Failed to mark message '{messageId}' as processed."));
        }
    }
}
