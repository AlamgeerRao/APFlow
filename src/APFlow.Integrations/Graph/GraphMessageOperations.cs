using APFlow.Application.DTOs;
using Microsoft.Graph;

namespace APFlow.Integrations.Graph;

/// <summary>
/// Thin seam between <see cref="EmailSyncService"/> and the Graph SDK. Same
/// testability reasoning as IGraphInboxReader (WP-004) and IBlobContainerOperations
/// (WP-005): this project cannot reliably fake Azure/Graph SDK client types without a
/// real package to verify against, so this interface is fully hand-written and owned
/// here, and kept mechanically as thin as possible - three direct pass-through Graph
/// calls, no merge/dedup logic. The category-merge logic that matters for correctness
/// (don't duplicate the processed category, don't clobber a message's other existing
/// categories) lives in EmailSyncService instead, specifically so it's covered by
/// real, fake-based unit tests rather than living in the untestable part of this
/// codebase.
/// </summary>
internal interface IGraphMessageOperations
{
    /// <summary>Returns metadata for every unread message in the given mailbox.</summary>
    Task<IReadOnlyList<EmailSummaryDto>> GetUnreadMessagesAsync(string mailboxUserPrincipalName, CancellationToken cancellationToken);

    /// <summary>Returns the current category list for the given message.</summary>
    Task<IReadOnlyList<string>> GetMessageCategoriesAsync(string mailboxUserPrincipalName, string messageId, CancellationToken cancellationToken);

    /// <summary>Replaces the given message's category list with exactly the provided set.</summary>
    Task SetMessageCategoriesAsync(string mailboxUserPrincipalName, string messageId, IReadOnlyList<string> categories, CancellationToken cancellationToken);
}

/// <summary>
/// Real implementation of <see cref="IGraphMessageOperations"/>, a direct wrapper
/// around Microsoft Graph. Deliberately mechanical - see the interface's doc comment
/// for why the merge logic lives elsewhere. NOT independently unit-tested in this
/// work package for the same reason as GraphInboxReader (WP-004) and
/// BlobContainerOperations (WP-005): doing so would require either a real tenant/
/// mailbox or faking Graph SDK client types this project cannot verify offline.
/// </summary>
internal sealed class GraphMessageOperations : IGraphMessageOperations
{
    private readonly GraphServiceClient _graphClient;

    public GraphMessageOperations(GraphServiceClient graphClient)
    {
        _graphClient = graphClient;
    }

    public async Task<IReadOnlyList<EmailSummaryDto>> GetUnreadMessagesAsync(string mailboxUserPrincipalName, CancellationToken cancellationToken)
    {
        var response = await _graphClient.Users[mailboxUserPrincipalName].Messages.GetAsync(config =>
        {
            config.QueryParameters.Filter = "isRead eq false";
            config.QueryParameters.Select = ["id", "subject", "from", "receivedDateTime"];
        }, cancellationToken);

        var messages = response?.Value ?? [];

        return messages.Select(message => new EmailSummaryDto(
            MessageId: message.Id ?? string.Empty,
            Subject: message.Subject ?? string.Empty,
            SenderAddress: message.From?.EmailAddress?.Address ?? string.Empty,
            SenderName: message.From?.EmailAddress?.Name,
            ReceivedAtUtc: message.ReceivedDateTime ?? DateTimeOffset.MinValue)).ToList();
    }

    public async Task<IReadOnlyList<string>> GetMessageCategoriesAsync(string mailboxUserPrincipalName, string messageId, CancellationToken cancellationToken)
    {
        var message = await _graphClient.Users[mailboxUserPrincipalName].Messages[messageId].GetAsync(config =>
        {
            config.QueryParameters.Select = ["categories"];
        }, cancellationToken);

        return message?.Categories ?? [];
    }

    public async Task SetMessageCategoriesAsync(string mailboxUserPrincipalName, string messageId, IReadOnlyList<string> categories, CancellationToken cancellationToken)
    {
        var update = new Microsoft.Graph.Models.Message
        {
            Categories = categories.ToList(),
        };

        await _graphClient.Users[mailboxUserPrincipalName].Messages[messageId].PatchAsync(update, cancellationToken: cancellationToken);
    }
}
