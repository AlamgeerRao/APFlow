using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace APFlow.Integrations.Graph;

/// <summary>
/// Graph-backed implementation of <see cref="IEmailSendService"/> (WP-031). Sends
/// from the same mailbox <see cref="EmailService"/>/<see cref="EmailSyncService"/>
/// already read from (<c>GraphOptions.MailboxUserPrincipalName</c>), via the same
/// app-only credential (<c>GraphOptions.ClientId</c>/<c>ClientSecret</c>/
/// <c>TenantId</c> - client-secret-or-Managed-Identity-fallback, unchanged from
/// WP-004) - the only new thing this WP needed at the Entra side was granting that
/// existing app registration the additional <c>Mail.Send</c> Application
/// permission (see docs/WP-031-Outbound-Supplier-Email-Report.md).
/// Depends on <see cref="IGraphMailSender"/> rather than <c>GraphServiceClient</c>
/// directly - same testability reasoning as EmailService/IGraphInboxReader (WP-004)
/// and GraphMessageOperations/IGraphMessageOperations (WP-006).
/// </summary>
public sealed class GraphEmailSendService : IEmailSendService
{
    private readonly IGraphMailSender _mailSender;
    private readonly GraphOptions _options;
    private readonly ILogger<GraphEmailSendService> _logger;

    /// <summary>Creates a new <see cref="GraphEmailSendService"/>.</summary>
    internal GraphEmailSendService(IGraphMailSender mailSender, IOptions<GraphOptions> options, ILogger<GraphEmailSendService> logger)
    {
        _mailSender = mailSender;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result> SendAsync(SendEmailRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.MailboxUserPrincipalName))
        {
            _logger.LogWarning("Graph:MailboxUserPrincipalName is not configured; cannot send email.");
            return Result.Failure(new Error("Email.MailboxNotConfigured", "The outbound mailbox is not configured."));
        }

        if (string.IsNullOrWhiteSpace(request.ToAddress))
        {
            return Result.Failure(new Error("Email.RecipientMissing", "An email recipient address is required."));
        }

        try
        {
            await _mailSender.SendMailAsync(_options.MailboxUserPrincipalName, request, cancellationToken);

            _logger.LogInformation("Sent email to {Recipient} from {Mailbox}.", request.ToAddress, _options.MailboxUserPrincipalName);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Caller-initiated cancellation, not a send failure - propagate as
            // cancellation rather than reporting a misleading "send failed".
            throw;
        }
        catch (Exception ex)
        {
            // Logged and returned as a failed Result, not rethrown - matches
            // IEmailSendService's documented contract ("never throws") so a caller
            // that considers a failed send non-fatal (InvoiceService.UpdateAsync)
            // can inspect the Result rather than needing a try/catch of its own.
            _logger.LogWarning(ex, "Failed to send email to {Recipient} from {Mailbox}.", request.ToAddress, _options.MailboxUserPrincipalName);
            return Result.Failure(new Error("Email.SendFailed", $"Failed to send email: {ex.Message}"));
        }
    }
}
