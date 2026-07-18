namespace APFlow.Application.Interfaces;

/// <summary>
/// Abstraction over the AP mailbox, backed by Microsoft Graph (see
/// APFlow.Integrations.Graph.EmailService). Deliberately minimal for WP-004: only
/// connection verification is implemented - "do not download emails" was explicitly
/// out of scope. Email retrieval, sending, and attachment handling are added here as
/// their own work packages land; this interface's shape should NOT be pre-guessed for
/// them now.
/// </summary>
public interface IEmailService
{
    /// <summary>
    /// Verifies the configured mailbox is reachable with the configured credentials,
    /// without reading any email content (a lightweight metadata-only call). Returns
    /// false rather than throwing on failure - this is a health/diagnostic check, not
    /// an operation whose failure should propagate as an exception.
    /// </summary>
    Task<bool> VerifyMailboxConnectionAsync(CancellationToken cancellationToken = default);
}
