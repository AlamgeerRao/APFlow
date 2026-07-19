using Microsoft.Graph;
using Microsoft.Graph.Models;

namespace APFlow.Integrations.Graph;

/// <summary>
/// Plain-C# projection of a Graph attachment, with no Graph SDK types - the seam
/// between the mechanical Graph fetch (<see cref="GraphAttachmentOperations"/>) and
/// the tested filtering/extraction logic (<see cref="PdfExtractionService"/>). Not
/// the same shape as the public <see cref="APFlow.Application.DTOs.PdfAttachmentDto"/>
/// deliberately - this carries fields (<see cref="IsInline"/>,
/// <see cref="IsFileAttachment"/>) that only matter internally for filtering
/// decisions, not to callers of the public API.
/// </summary>
internal sealed record GraphAttachmentInfo(
    string FileName,
    long SizeInBytes,
    string ContentType,
    bool IsInline,
    bool IsFileAttachment,
    byte[]? Content);

/// <summary>
/// Thin seam between <see cref="PdfExtractionService"/> and the Graph SDK. Same
/// testability reasoning as IGraphInboxReader (WP-004), IBlobContainerOperations
/// (WP-005), and IGraphMessageOperations (WP-006): this project cannot reliably fake
/// Graph SDK client types without a real package to verify against, so this interface
/// is fully hand-written and owned here, and kept mechanically thin - one Graph call,
/// mapped to plain C#. All PDF-detection, inline-skipping, and unsupported-type
/// filtering logic lives in PdfExtractionService instead, specifically so it's
/// covered by real, fake-based unit tests.
/// </summary>
internal interface IGraphAttachmentOperations
{
    /// <summary>Returns every attachment (of any type) on the given message, unfiltered.</summary>
    Task<IReadOnlyList<GraphAttachmentInfo>> GetAttachmentsAsync(string mailboxUserPrincipalName, string messageId, CancellationToken cancellationToken);
}

/// <summary>
/// Real implementation of <see cref="IGraphAttachmentOperations"/>, a direct wrapper
/// around Microsoft Graph. Deliberately mechanical - see the interface's doc comment
/// for why the filtering logic lives elsewhere. NOT independently unit-tested in this
/// work package, for the same reason as GraphMessageOperations (WP-006): doing so
/// would require either a real tenant/mailbox or faking Graph SDK client types this
/// project cannot verify offline.
/// PERMISSIONS: this is a read-only call (GET) - no write permission beyond what
/// WP-004/WP-006 already established (Mail.ReadWrite, widened in WP-006) is required.
/// No further update to docs/WP-004-Graph-Verification-Checklist.md is needed for
/// this work package.
/// </summary>
internal sealed class GraphAttachmentOperations : IGraphAttachmentOperations
{
    private readonly GraphServiceClient _graphClient;

    public GraphAttachmentOperations(GraphServiceClient graphClient)
    {
        _graphClient = graphClient;
    }

    public async Task<IReadOnlyList<GraphAttachmentInfo>> GetAttachmentsAsync(string mailboxUserPrincipalName, string messageId, CancellationToken cancellationToken)
    {
        var response = await _graphClient.Users[mailboxUserPrincipalName].Messages[messageId].Attachments.GetAsync(
            cancellationToken: cancellationToken);

        var attachments = response?.Value ?? [];

        return attachments.Select(attachment => new GraphAttachmentInfo(
            FileName: attachment.Name ?? string.Empty,
            SizeInBytes: attachment.Size ?? 0,
            ContentType: attachment.ContentType ?? string.Empty,
            IsInline: attachment.IsInline ?? false,
            IsFileAttachment: attachment is FileAttachment,
            Content: (attachment as FileAttachment)?.ContentBytes)).ToList();
    }
}
