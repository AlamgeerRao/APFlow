using APFlow.Application.DTOs;
using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Sends a plain email from the configured AP mailbox (see
/// APFlow.Integrations.Graph.GraphEmailSendService), via Microsoft Graph's
/// application-permission <c>sendMail</c> action. WP-031 scope: a small, generic
/// "send this subject/body to this recipient, from the mailbox we already read
/// from" capability - deliberately NOT shaped around WP-031's own first caller
/// (the outbound supplier query email), so WP-040 (remittance email, a separate
/// future work package) can construct its own <see cref="SendEmailRequest"/> and
/// call this same interface unchanged, per WP-031 Task 5. Plain-text body only -
/// no attachment support and no HTML formatting, since neither WP-031's query
/// email nor WP-040's plan-level scope (docs/Sprint2-Plan.md §3 WP-040) calls for
/// either yet; adding them later is a non-breaking extension of
/// <see cref="SendEmailRequest"/>, not a reason to speculatively build them now.
/// </summary>
public interface IEmailSendService
{
    /// <summary>
    /// Sends the given email from the configured mailbox (<c>GraphOptions.MailboxUserPrincipalName</c>).
    /// Returns a failed <see cref="Result"/> - never throws - if the send could not
    /// be completed (e.g. Graph unreachable, permission missing/not consented,
    /// invalid recipient address), so a caller that considers a failed send
    /// non-fatal to its own already-committed operation (e.g.
    /// <c>InvoiceService.UpdateAsync</c>'s query-raised transition, WP-031) can log
    /// a warning and continue rather than unwind work that already succeeded.
    /// </summary>
    Task<Result> SendAsync(SendEmailRequest request, CancellationToken cancellationToken = default);
}
