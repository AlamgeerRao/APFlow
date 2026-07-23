using APFlow.Application.Interfaces;
using APFlow.Domain.Common;

namespace APFlow.Application.Tests.Features;

/// <summary>
/// Hand-written fake, same pattern as every fake elsewhere in this codebase.
/// Defaults to "always authorized" so existing tests that don't care about
/// approval-gating aren't affected by this dependency's introduction.
/// </summary>
internal sealed class FakeApprovalAuthorizationService : IApprovalAuthorizationService
{
    public Func<string, IReadOnlyCollection<string>, Result>? ResultFactory { get; set; }

    public Task<Result> AuthorizeAsync(string domain, IReadOnlyCollection<string> actingUserRoles, CancellationToken cancellationToken = default) =>
        Task.FromResult(ResultFactory?.Invoke(domain, actingUserRoles) ?? Result.Success());
}
