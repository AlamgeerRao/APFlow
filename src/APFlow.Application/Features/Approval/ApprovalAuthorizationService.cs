using APFlow.Application.Interfaces;
using APFlow.Domain.Common;

namespace APFlow.Application.Features.Approval;

/// <summary>
/// Default implementation of <see cref="IApprovalAuthorizationService"/>. Depends
/// only on <see cref="IApprovalPolicyRepository"/>, so this class is fully
/// unit-testable with a fake repository.
/// </summary>
public sealed class ApprovalAuthorizationService : IApprovalAuthorizationService
{
    private readonly IApprovalPolicyRepository _repository;

    /// <summary>Creates a new <see cref="ApprovalAuthorizationService"/>.</summary>
    public ApprovalAuthorizationService(IApprovalPolicyRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<Result> AuthorizeAsync(
        string domain, IReadOnlyCollection<string> actingUserRoles, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domain))
        {
            return Result.Failure(new Error("Approval.InvalidDomain", "Domain must not be empty."));
        }

        var policy = await _repository.GetActivePolicyAsync(domain, cancellationToken);
        if (policy is null)
        {
            // Fail closed: no configured policy is not the same as "no restriction".
            return Result.Failure(new Error(
                "Approval.PolicyNotConfigured", $"No approval policy is configured for domain '{domain}'."));
        }

        return actingUserRoles.Contains(policy.RequiredRole)
            ? Result.Success()
            : Result.Failure(new Error(
                "Approval.Unauthorized", $"This action requires the '{policy.RequiredRole}' role."));
    }
}
