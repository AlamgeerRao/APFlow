using APFlow.Application.DTOs;
using APFlow.Integrations.Graph;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace APFlow.Integrations.Tests.Graph;

public class GraphEmailSendServiceTests
{
    [Fact]
    public async Task SendAsync_MailboxNotConfigured_ReturnsFailure_WithoutCallingSender()
    {
        var sender = new FakeGraphMailSender(behavior: SenderBehavior.ThrowIfCalled);
        var service = CreateService(sender, mailboxUpn: "");

        var result = await service.SendAsync(new SendEmailRequest("supplier@example.com", "Subject", "Body"));

        Assert.True(result.IsFailure);
        Assert.Equal("Email.MailboxNotConfigured", result.Error.Code);
    }

    [Fact]
    public async Task SendAsync_NoRecipientAddress_ReturnsFailure_WithoutCallingSender()
    {
        var sender = new FakeGraphMailSender(behavior: SenderBehavior.ThrowIfCalled);
        var service = CreateService(sender, mailboxUpn: "invoices@example.com");

        var result = await service.SendAsync(new SendEmailRequest("", "Subject", "Body"));

        Assert.True(result.IsFailure);
        Assert.Equal("Email.RecipientMissing", result.Error.Code);
    }

    [Fact]
    public async Task SendAsync_SenderSucceeds_ReturnsSuccess_PassesRequestThrough()
    {
        var sender = new FakeGraphMailSender(behavior: SenderBehavior.Succeed);
        var service = CreateService(sender, mailboxUpn: "invoices@example.com");
        var request = new SendEmailRequest("supplier@example.com", "Query re: INV-1", "Please respond.", "Acme Ltd");

        var result = await service.SendAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("invoices@example.com", sender.LastMailboxUpn);
        Assert.Same(request, sender.LastRequest);
    }

    [Fact]
    public async Task SendAsync_SenderThrows_ReturnsFailure_DoesNotPropagateException()
    {
        // Same "swallow and report as a failed Result, never throw" contract as
        // EmailService.VerifyMailboxConnectionAsync (WP-004) - here surfaced as a
        // failed Result rather than `false`, since SendAsync is an actual operation
        // (see IEmailSendService's doc comment), but the "don't let a Graph SDK
        // exception escape to the caller" behavior is the same.
        var sender = new FakeGraphMailSender(behavior: SenderBehavior.Throw);
        var service = CreateService(sender, mailboxUpn: "invoices@example.com");

        var result = await service.SendAsync(new SendEmailRequest("supplier@example.com", "Subject", "Body"));

        Assert.True(result.IsFailure);
        Assert.Equal("Email.SendFailed", result.Error.Code);
    }

    [Fact]
    public async Task SendAsync_CallerCancels_PropagatesCancellation_DoesNotReturnFailedResult()
    {
        var sender = new FakeGraphMailSender(behavior: SenderBehavior.ThrowOperationCanceled);
        var service = CreateService(sender, mailboxUpn: "invoices@example.com");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => service.SendAsync(new SendEmailRequest("supplier@example.com", "Subject", "Body"), cts.Token));
    }

    private static GraphEmailSendService CreateService(IGraphMailSender sender, string mailboxUpn)
    {
        var options = Options.Create(new GraphOptions
        {
            TenantId = "fake-tenant",
            ClientId = "fake-client",
            MailboxUserPrincipalName = mailboxUpn,
        });

        return new GraphEmailSendService(sender, options, NullLogger<GraphEmailSendService>.Instance);
    }

    private enum SenderBehavior
    {
        Succeed,
        Throw,
        ThrowOperationCanceled,
        ThrowIfCalled,
    }

    private sealed class FakeGraphMailSender : IGraphMailSender
    {
        private readonly SenderBehavior _behavior;

        public FakeGraphMailSender(SenderBehavior behavior)
        {
            _behavior = behavior;
        }

        public string? LastMailboxUpn { get; private set; }
        public SendEmailRequest? LastRequest { get; private set; }

        public Task SendMailAsync(string mailboxUserPrincipalName, SendEmailRequest request, CancellationToken cancellationToken)
        {
            LastMailboxUpn = mailboxUserPrincipalName;
            LastRequest = request;

            return _behavior switch
            {
                SenderBehavior.Succeed => Task.CompletedTask,
                SenderBehavior.Throw => throw new InvalidOperationException("Simulated Graph failure."),
                SenderBehavior.ThrowOperationCanceled => throw new OperationCanceledException(cancellationToken),
                SenderBehavior.ThrowIfCalled => throw new InvalidOperationException(
                    "IGraphMailSender should not be called when the mailbox is not configured or the recipient is missing."),
                _ => throw new ArgumentOutOfRangeException(nameof(_behavior)),
            };
        }
    }
}
