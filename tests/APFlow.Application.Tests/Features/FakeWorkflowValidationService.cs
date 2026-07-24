using APFlow.Application.Interfaces;
using APFlow.Domain.Common;

namespace APFlow.Application.Tests.Features;

/// <summary>
/// Hand-written fake, same pattern as every fake elsewhere in this codebase.
/// Defaults to "every transition allowed" so pre-WP-053 tests that don't care
/// about transition validation aren't affected by this dependency's introduction -
/// tests that DO care (see InvoiceServiceTests' WP-053 tests) either set
/// <see cref="ResultFactory"/> explicitly or use the real
/// <c>WorkflowValidationService</c> backed by a fake template repository.
/// </summary>
internal sealed class FakeWorkflowValidationService : IWorkflowValidationService
{
    public Func<string, string, string, Result>? ResultFactory { get; set; }

    public Task<Result> ValidateTransitionAsync(
        string domainName, string fromStatusCode, string toStatusCode, CancellationToken cancellationToken = default) =>
        Task.FromResult(ResultFactory?.Invoke(domainName, fromStatusCode, toStatusCode) ?? Result.Success());
}
