using APFlow.Application.Interfaces;
using APFlow.Domain.Common.Constants;

namespace APFlow.Application.Features.Invoices;

/// <summary>
/// Default implementation of <see cref="IDuplicateOverrideAuthorizationService"/>.
/// The entire, isolated answer to "who may dismiss a duplicate warning" lives here -
/// see the interface's doc comment for why that isolation matters. Currently:
/// <see cref="Roles.FinanceManager"/> only.
/// </summary>
public sealed class DuplicateOverrideAuthorizationService : IDuplicateOverrideAuthorizationService
{
    /// <inheritdoc />
    public bool CanOverrideDuplicateWarning(IReadOnlyCollection<string> roles) =>
        roles.Contains(Roles.FinanceManager);
}
