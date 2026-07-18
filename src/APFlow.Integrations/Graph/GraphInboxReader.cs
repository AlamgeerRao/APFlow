using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace APFlow.Integrations.Graph;

/// <summary>
/// Thin seam between <see cref="EmailService"/> and the Graph SDK. Exists specifically
/// for testability: <see cref="GraphServiceClient"/>'s fluent Kiota-generated builder
/// (and the underlying <c>IRequestAdapter</c> it depends on) is not something this
/// project can reliably fake without a real Microsoft.Graph package to verify against -
/// this one-method interface is fully hand-written and owned here instead, so
/// EmailServiceTests can fake it directly with full confidence it compiles and behaves
/// as intended. Internal: not part of the Application-facing abstraction
/// (<see cref="APFlow.Application.Interfaces.IEmailService"/>) - only EmailService and
/// its tests should depend on this.
/// </summary>
internal interface IGraphInboxReader
{
    /// <summary>
    /// Returns the inbox folder metadata for the given mailbox, or null/throws on
    /// failure - see <see cref="GraphInboxReader"/> for the real implementation's
    /// exact behavior.
    /// </summary>
    Task<MailFolder?> GetInboxAsync(string mailboxUserPrincipalName, CancellationToken cancellationToken);
}

/// <summary>
/// Real implementation of <see cref="IGraphInboxReader"/>, a direct pass-through to
/// Microsoft Graph. Deliberately trivial (one line) - all logic worth testing lives in
/// <see cref="EmailService"/>, which depends on the interface, not this class.
/// </summary>
internal sealed class GraphInboxReader : IGraphInboxReader
{
    private readonly GraphServiceClient _graphClient;

    public GraphInboxReader(GraphServiceClient graphClient)
    {
        _graphClient = graphClient;
    }

    public Task<MailFolder?> GetInboxAsync(string mailboxUserPrincipalName, CancellationToken cancellationToken) =>
        _graphClient.Users[mailboxUserPrincipalName].MailFolders["inbox"].GetAsync(cancellationToken: cancellationToken);
}
