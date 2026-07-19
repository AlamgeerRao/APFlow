using APFlow.Integrations.Graph;

namespace APFlow.Integrations.Tests.Graph;

/// <summary>
/// Hand-written fake for <see cref="IGraphAttachmentOperations"/>, fully controlled
/// by this test project - same reasoning as the fakes in WP-004/005/006.
/// </summary>
internal sealed class FakeGraphAttachmentOperations : IGraphAttachmentOperations
{
    public enum Behavior
    {
        Succeed,
        ThrowGeneric,
        ThrowOperationCanceled,
    }

    public Behavior Mode { get; set; } = Behavior.Succeed;
    public IReadOnlyList<GraphAttachmentInfo> Attachments { get; set; } = [];

    public Task<IReadOnlyList<GraphAttachmentInfo>> GetAttachmentsAsync(string mailboxUserPrincipalName, string messageId, CancellationToken cancellationToken) => Mode switch
    {
        Behavior.Succeed => Task.FromResult(Attachments),
        Behavior.ThrowOperationCanceled => throw new OperationCanceledException(cancellationToken),
        _ => throw new InvalidOperationException("Simulated attachment fetch failure."),
    };
}
