using APFlow.Application.DTOs;
using APFlow.Integrations.Graph;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace APFlow.Integrations.Tests.Graph;

public class EmailSyncServiceTests
{
    // --- SyncUnreadEmailsAsync ------------------------------------------------

    [Fact]
    public async Task SyncUnreadEmailsAsync_MailboxNotConfigured_ReturnsFailure()
    {
        var (service, _) = CreateService(mailboxUpn: "");

        var result = await service.SyncUnreadEmailsAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("EmailSync.MailboxNotConfigured", result.Error.Code);
    }

    [Fact]
    public async Task SyncUnreadEmailsAsync_Success_ReturnsMessages()
    {
        var (service, ops) = CreateService();
        ops.UnreadMessages =
        [
            new EmailSummaryDto("msg-1", "Invoice attached", "vendor@example.com", "Vendor Co", DateTimeOffset.UtcNow),
        ];

        var result = await service.SyncUnreadEmailsAsync();

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
        Assert.Equal("msg-1", result.Value[0].MessageId);
    }

    [Fact]
    public async Task SyncUnreadEmailsAsync_GraphFails_ReturnsFailure_DoesNotPropagate()
    {
        var (service, ops) = CreateService();
        ops.Mode = FakeGraphMessageOperations.Behavior.ThrowGeneric;

        var result = await service.SyncUnreadEmailsAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("EmailSync.SyncFailed", result.Error.Code);
    }

    [Fact]
    public async Task SyncUnreadEmailsAsync_CallerCancels_PropagatesCancellation()
    {
        var (service, ops) = CreateService();
        ops.Mode = FakeGraphMessageOperations.Behavior.ThrowOperationCanceled;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SyncUnreadEmailsAsync(cts.Token));
    }

    // --- MarkAsProcessedAsync --------------------------------------------------

    [Fact]
    public async Task MarkAsProcessedAsync_EmptyMessageId_ReturnsFailure_WithoutCallingGraph()
    {
        var (service, ops) = CreateService();

        var result = await service.MarkAsProcessedAsync("");

        Assert.True(result.IsFailure);
        Assert.Equal("EmailSync.InvalidMessageId", result.Error.Code);
        Assert.False(ops.SetMessageCategoriesCalled);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_MailboxNotConfigured_ReturnsFailure()
    {
        var (service, _) = CreateService(mailboxUpn: "");

        var result = await service.MarkAsProcessedAsync("msg-1");

        Assert.True(result.IsFailure);
        Assert.Equal("EmailSync.MailboxNotConfigured", result.Error.Code);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_NoExistingCategories_AddsProcessedCategory()
    {
        var (service, ops) = CreateService();
        ops.ExistingCategories = [];

        var result = await service.MarkAsProcessedAsync("msg-1");

        Assert.True(result.IsSuccess);
        Assert.True(ops.SetMessageCategoriesCalled);
        Assert.Equal(["APFlow-Processed"], ops.LastSetCategories);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_HasOtherExistingCategories_PreservesThem()
    {
        // Graph's PATCH replaces the entire categories array - this proves the merge
        // happens correctly rather than clobbering the user's own Outlook categories.
        var (service, ops) = CreateService();
        ops.ExistingCategories = ["Red Category", "Follow Up"];

        var result = await service.MarkAsProcessedAsync("msg-1");

        Assert.True(result.IsSuccess);
        Assert.NotNull(ops.LastSetCategories);
        Assert.Contains("Red Category", ops.LastSetCategories);
        Assert.Contains("Follow Up", ops.LastSetCategories);
        Assert.Contains("APFlow-Processed", ops.LastSetCategories);
        Assert.Equal(3, ops.LastSetCategories.Count);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_AlreadyMarked_IsIdempotent_DoesNotCallSet()
    {
        var (service, ops) = CreateService();
        ops.ExistingCategories = ["APFlow-Processed"];

        var result = await service.MarkAsProcessedAsync("msg-1");

        Assert.True(result.IsSuccess);
        Assert.False(ops.SetMessageCategoriesCalled);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_AlreadyMarked_IsCaseInsensitive()
    {
        var (service, ops) = CreateService();
        ops.ExistingCategories = ["apflow-processed"]; // different case than configured "APFlow-Processed"

        var result = await service.MarkAsProcessedAsync("msg-1");

        Assert.True(result.IsSuccess);
        Assert.False(ops.SetMessageCategoriesCalled);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_GetCategoriesFails_ReturnsFailure()
    {
        var (service, ops) = CreateService();
        ops.Mode = FakeGraphMessageOperations.Behavior.ThrowGeneric;

        var result = await service.MarkAsProcessedAsync("msg-1");

        Assert.True(result.IsFailure);
        Assert.Equal("EmailSync.MarkProcessedFailed", result.Error.Code);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_CallerCancels_PropagatesCancellation()
    {
        var (service, ops) = CreateService();
        ops.Mode = FakeGraphMessageOperations.Behavior.ThrowOperationCanceled;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.MarkAsProcessedAsync("msg-1", cts.Token));
    }

    private static (EmailSyncService Service, FakeGraphMessageOperations Operations) CreateService(string mailboxUpn = "ap-invoices@example.com")
    {
        var operations = new FakeGraphMessageOperations();
        var options = Options.Create(new GraphOptions
        {
            TenantId = "fake-tenant",
            ClientId = "fake-client",
            MailboxUserPrincipalName = mailboxUpn,
        });

        var service = new EmailSyncService(operations, options, NullLogger<EmailSyncService>.Instance);
        return (service, operations);
    }
}
