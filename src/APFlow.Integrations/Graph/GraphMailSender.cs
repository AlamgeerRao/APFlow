using APFlow.Application.DTOs;
using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace APFlow.Integrations.Graph;

/// <summary>
/// Thin seam between <see cref="GraphEmailSendService"/> and the Graph SDK. Same
/// testability reasoning as IGraphInboxReader (WP-004) and IGraphMessageOperations
/// (WP-006): the Graph SDK's fluent Kiota-generated builder can't reliably be faked
/// without a real package to verify against, so this one-method interface is fully
/// hand-written and owned here, and kept mechanically thin - a single pass-through
/// call, no retry/error-shaping logic (that lives in GraphEmailSendService, so it's
/// covered by real, fake-based unit tests). Internal: not part of the
/// Application-facing abstraction (<see cref="APFlow.Application.Interfaces.IEmailSendService"/>) -
/// only GraphEmailSendService and its tests should depend on this.
/// </summary>
internal interface IGraphMailSender
{
    /// <summary>
    /// Sends the given email from the given mailbox via Graph's application-permission
    /// <c>sendMail</c> action. Throws on failure (Graph SDK convention) - error
    /// shaping into a <see cref="APFlow.Domain.Common.Result"/> happens in
    /// <see cref="GraphEmailSendService"/>, not here.
    /// </summary>
    Task SendMailAsync(string mailboxUserPrincipalName, SendEmailRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Real implementation of <see cref="IGraphMailSender"/>, a direct wrapper around
/// Microsoft Graph. Deliberately trivial (one call) - see the interface's doc
/// comment for why. NOT independently unit-tested in this work package, for the
/// same reason as GraphInboxReader (WP-004) and GraphMessageOperations (WP-006):
/// doing so would require either a real tenant/mailbox or faking Graph SDK client
/// types this project cannot verify offline.
/// </summary>
internal sealed class GraphMailSender : IGraphMailSender
{
    private readonly GraphServiceClient _graphClient;

    public GraphMailSender(GraphServiceClient graphClient)
    {
        _graphClient = graphClient;
    }

    public Task SendMailAsync(string mailboxUserPrincipalName, SendEmailRequest request, CancellationToken cancellationToken)
    {
        var message = new Message
        {
            Subject = request.Subject,
            Body = new ItemBody
            {
                ContentType = BodyType.Text,
                Content = request.Body,
            },
            ToRecipients =
            [
                new Recipient
                {
                    EmailAddress = new EmailAddress
                    {
                        Address = request.ToAddress,
                        Name = request.ToDisplayName,
                    },
                },
            ],
        };

        var body = new Microsoft.Graph.Users.Item.SendMail.SendMailPostRequestBody
        {
            Message = message,
            SaveToSentItems = true,
        };

        return _graphClient.Users[mailboxUserPrincipalName].SendMail.PostAsync(body, cancellationToken: cancellationToken);
    }
}
