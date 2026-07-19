using APFlow.Application.DTOs;
using APFlow.Integrations.Graph;

namespace APFlow.Integrations.Tests.Graph;

/// <summary>
/// Hand-written fake for <see cref="IGraphMessageOperations"/>, fully controlled by
/// this test project - same reasoning as FakeGraphInboxReader (WP-004) and
/// FakeBlobContainerOperations (WP-005).
/// </summary>
internal sealed class FakeGraphMessageOperations : IGraphMessageOperations
{
    public enum Behavior
    {
        Succeed,
        ThrowGeneric,
        ThrowOperationCanceled,
    }

    public Behavior Mode { get; set; } = Behavior.Succeed;
    public IReadOnlyList<EmailSummaryDto> UnreadMessages { get; set; } = [];
    public IReadOnlyList<string> ExistingCategories { get; set; } = [];
    public IReadOnlyList<string>? LastSetCategories { get; private set; }
    public bool SetMessageCategoriesCalled { get; private set; }

    public Task<IReadOnlyList<EmailSummaryDto>> GetUnreadMessagesAsync(string mailboxUserPrincipalName, CancellationToken cancellationToken) => Mode switch
    {
        Behavior.Succeed => Task.FromResult(UnreadMessages),
        Behavior.ThrowOperationCanceled => throw new OperationCanceledException(cancellationToken),
        _ => throw new InvalidOperationException("Simulated sync failure."),
    };

    public Task<IReadOnlyList<string>> GetMessageCategoriesAsync(string mailboxUserPrincipalName, string messageId, CancellationToken cancellationToken) => Mode switch
    {
        Behavior.Succeed => Task.FromResult(ExistingCategories),
        Behavior.ThrowOperationCanceled => throw new OperationCanceledException(cancellationToken),
        _ => throw new InvalidOperationException("Simulated get-categories failure."),
    };

    public Task SetMessageCategoriesAsync(string mailboxUserPrincipalName, string messageId, IReadOnlyList<string> categories, CancellationToken cancellationToken)
    {
        SetMessageCategoriesCalled = true;
        LastSetCategories = categories;

        return Mode switch
        {
            Behavior.Succeed => Task.CompletedTask,
            Behavior.ThrowOperationCanceled => throw new OperationCanceledException(cancellationToken),
            _ => throw new InvalidOperationException("Simulated set-categories failure."),
        };
    }
}
