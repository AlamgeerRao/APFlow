using APFlow.Domain.Entities;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Data access abstraction for <see cref="ApprovalPolicy"/> (WP-051). Plain Domain
/// types only, same design as <see cref="IWorkflowTemplateRepository"/>.
/// </summary>
public interface IApprovalPolicyRepository
{
    /// <summary>
    /// Returns the ACTIVE policy for the given domain (e.g. "InvoiceApproval") and
    /// the current tenant: that tenant's own policy if one exists, otherwise the
    /// platform-default policy (<c>TenantId == null</c>), otherwise null (no policy
    /// configured for this domain at all).
    /// </summary>
    Task<ApprovalPolicy?> GetActivePolicyAsync(string domain, CancellationToken cancellationToken = default);
}
