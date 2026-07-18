using APFlow.Integrations.Graph;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Graph.Models;
using Xunit;

namespace APFlow.Integrations.Tests.Graph;

public class EmailServiceTests
{
    [Fact]
    public async Task VerifyMailboxConnectionAsync_MailboxNotConfigured_ReturnsFalse_WithoutCallingReader()
    {
        var reader = new FakeGraphInboxReader(behavior: ReaderBehavior.ThrowIfCalled);
        var service = CreateService(reader, mailboxUpn: "");

        var result = await service.VerifyMailboxConnectionAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyMailboxConnectionAsync_ReaderReturnsFolder_ReturnsTrue()
    {
        var reader = new FakeGraphInboxReader(behavior: ReaderBehavior.ReturnFolder);
        var service = CreateService(reader, mailboxUpn: "ap-invoices@example.com");

        var result = await service.VerifyMailboxConnectionAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task VerifyMailboxConnectionAsync_ReaderReturnsNull_ReturnsFalse()
    {
        var reader = new FakeGraphInboxReader(behavior: ReaderBehavior.ReturnNull);
        var service = CreateService(reader, mailboxUpn: "ap-invoices@example.com");

        var result = await service.VerifyMailboxConnectionAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyMailboxConnectionAsync_ReaderThrows_ReturnsFalse_DoesNotPropagateException()
    {
        // This is the core, previously-untested contract of this method: a Graph
        // failure (auth error, revoked permission, network issue - anything the SDK
        // surfaces as an exception) must be swallowed and reported as `false`, not
        // thrown to the caller. GraphMailboxHealthCheck and any future caller depends
        // on this never throwing.
        var reader = new FakeGraphInboxReader(behavior: ReaderBehavior.Throw);
        var service = CreateService(reader, mailboxUpn: "ap-invoices@example.com");

        var result = await service.VerifyMailboxConnectionAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task VerifyMailboxConnectionAsync_CallerCancels_PropagatesCancellation_DoesNotReturnFalse()
    {
        // A caller-initiated cancellation (e.g. a health check timeout) is distinct
        // from a real Graph failure and must propagate as cancellation, not be
        // reported as "mailbox unreachable" - see EmailService's dedicated catch
        // clause for OperationCanceledException.
        var reader = new FakeGraphInboxReader(behavior: ReaderBehavior.ThrowOperationCanceled);
        var service = CreateService(reader, mailboxUpn: "ap-invoices@example.com");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.VerifyMailboxConnectionAsync(cts.Token));
    }

    private static EmailService CreateService(IGraphInboxReader reader, string mailboxUpn)
    {
        var options = Options.Create(new GraphOptions
        {
            TenantId = "fake-tenant",
            ClientId = "fake-client",
            MailboxUserPrincipalName = mailboxUpn,
        });

        return new EmailService(reader, options, NullLogger<EmailService>.Instance);
    }

    private enum ReaderBehavior
    {
        ReturnFolder,
        ReturnNull,
        Throw,
        ThrowOperationCanceled,
        ThrowIfCalled,
    }

    private sealed class FakeGraphInboxReader : IGraphInboxReader
    {
        private readonly ReaderBehavior _behavior;

        public FakeGraphInboxReader(ReaderBehavior behavior)
        {
            _behavior = behavior;
        }

        public Task<MailFolder?> GetInboxAsync(string mailboxUserPrincipalName, CancellationToken cancellationToken) => _behavior switch
        {
            ReaderBehavior.ReturnFolder => Task.FromResult<MailFolder?>(new MailFolder()),
            ReaderBehavior.ReturnNull => Task.FromResult<MailFolder?>(null),
            ReaderBehavior.Throw => throw new InvalidOperationException("Simulated Graph failure."),
            ReaderBehavior.ThrowOperationCanceled => throw new OperationCanceledException(cancellationToken),
            ReaderBehavior.ThrowIfCalled => throw new InvalidOperationException(
                "IGraphInboxReader should not be called when the mailbox is not configured."),
            _ => throw new ArgumentOutOfRangeException(nameof(_behavior)),
        };
    }
}
