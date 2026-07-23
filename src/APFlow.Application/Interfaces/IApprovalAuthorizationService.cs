using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Checks whether an acting user is authorized to perform an approval-type action
/// within a domain (WP-051), per the acting tenant's active <c>ApprovalPolicy</c>.
/// A single, named, swappable check - same reasoning as WP-047's
/// <see cref="IDuplicateOverrideAuthorizationService"/>: the answer is data
/// (<c>ApprovalPolicy.RequiredRole</c>), not an inline role-string comparison
/// scattered across every caller that needs this check.
/// Intended callers: <c>InvoiceService.UpdateAsync</c> (wired - task 4, domain
/// <see cref="APFlow.Domain.Common.Constants.ApprovalDomains.InvoiceApproval"/>)
/// and a future WP-038 remittance/payment-batch-creation feature (documented, not
/// wired - task 5, domain
/// <see cref="APFlow.Domain.Common.Constants.ApprovalDomains.PaymentBatchApproval"/>).
/// </summary>
public interface IApprovalAuthorizationService
{
    /// <summary>
    /// Returns a failure if none of <paramref name="actingUserRoles"/> matches the
    /// active <c>ApprovalPolicy.RequiredRole</c> for <paramref name="domain"/>, or
    /// if no policy is configured for that domain at all (fails closed - an
    /// unconfigured policy is not treated as "no restriction"). Returns success if
    /// authorized.
    /// </summary>
    Task<Result> AuthorizeAsync(string domain, IReadOnlyCollection<string> actingUserRoles, CancellationToken cancellationToken = default);
}
