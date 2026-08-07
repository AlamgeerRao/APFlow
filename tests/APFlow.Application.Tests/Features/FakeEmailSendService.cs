using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;

namespace APFlow.Application.Tests.Features;

/// <summary>
/// Hand-written fake, same pattern as every fake elsewhere in this codebase (e.g.
/// <see cref="FakeApprovalAuthorizationService"/>). Records every send it was asked
/// to make, so WP-031 tests can assert on the exact <see cref="SendEmailRequest"/>
/// InvoiceService built (recipient/subject/body), without a real Graph call.
/// Defaults to "always succeeds" so existing tests that don't care about email
/// sending aren't affected by this dependency's introduction.
/// </summary>
internal sealed class FakeEmailSendService : IEmailSendService
{
    public List<SendEmailRequest> SentEmails { get; } = [];

    public Func<SendEmailRequest, Result>? ResultFactory { get; set; }

    public Task<Result> SendAsync(SendEmailRequest request, CancellationToken cancellationToken = default)
    {
        SentEmails.Add(request);
        return Task.FromResult(ResultFactory?.Invoke(request) ?? Result.Success());
    }
}
