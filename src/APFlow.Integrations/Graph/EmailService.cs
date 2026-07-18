using APFlow.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APFlow.Integrations.Graph;

/// <summary>
/// Graph-backed implementation of <see cref="IEmailService"/>. WP-004 scope only:
/// connection verification via a lightweight metadata-only call. Does not read email
/// content - "do not download emails" was explicit WP-004 scope. Email retrieval is a
/// future work package.
/// Depends on <see cref="IGraphInboxReader"/> rather than <c>GraphServiceClient</c>
/// directly - see that interface's doc comment for why (testability of the paths
/// below without needing to fake the Graph SDK's internals).
/// </summary>
public sealed class EmailService : IEmailService
{
    private readonly IGraphInboxReader _inboxReader;
    private readonly GraphOptions _options;
    private readonly ILogger<EmailService> _logger;

    /// <summary>Creates a new <see cref="EmailService"/>.</summary>
    internal EmailService(IGraphInboxReader inboxReader, IOptions<GraphOptions> options, ILogger<EmailService> logger)
    {
        _inboxReader = inboxReader;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> VerifyMailboxConnectionAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.MailboxUserPrincipalName))
        {
            _logger.LogWarning("Graph:MailboxUserPrincipalName is not configured; cannot verify mailbox connection.");
            return false;
        }

        try
        {
            // GET /users/{upn}/mailFolders/inbox - returns folder metadata (name,
            // item counts) only. Deliberately not listing/reading messages: this is a
            // connectivity/permission check, not an email read (WP-004: "do not
            // download emails").
            var inbox = await _inboxReader.GetInboxAsync(_options.MailboxUserPrincipalName, cancellationToken);

            return inbox is not null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A caller-initiated cancellation (e.g. a health check timeout) is not
            // "the mailbox is broken" - propagate it as cancellation, don't conflate
            // it with a real connectivity failure by returning false and logging a
            // misleading "unreachable" warning.
            throw;
        }
        catch (Exception ex)
        {
            // Logged, not rethrown: this is a verification/diagnostic method whose
            // contract is "return whether it's reachable", not "throw on failure".
            // Callers needing to distinguish failure reasons (auth vs. not-found vs.
            // network) should inspect logs, not catch exceptions from this method.
            _logger.LogWarning(ex, "Mailbox connection verification failed for {Mailbox}.", _options.MailboxUserPrincipalName);
            return false;
        }
    }
}
