using APFlow.Application.DTOs;
using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Computes, for a given invoice, the workflow transitions the ACTING USER may
/// actually execute right now (WP-054): the acting tenant's active
/// <c>WorkflowTemplate</c> (WP-050), filtered down to edges leaving the
/// invoice's current status, further filtered to only those the caller's roles
/// satisfy. Deliberately not "the full graph with permission flags" - this
/// matches WP-018's own "don't render a button that will always be rejected"
/// approach and avoids duplicating role-policy logic on the client.
///
/// <para>
/// Composes the exact same building blocks <c>InvoiceService.UpdateAsync</c>
/// itself already enforces (WP-053) -
/// <see cref="IWorkflowQueryService"/> for the confirmed graph,
/// <see cref="APFlow.Domain.Common.Constants.RoleGatedTransitions"/> for which
/// edges are gated, and <see cref="IApprovalAuthorizationService"/> for the
/// role check itself - so nothing this service lists can then be rejected by
/// that update path for a reason this service could have already known about.
/// No new business logic is introduced here; this is a read-side reflection of
/// what the write path would accept.
/// </para>
/// </summary>
public interface IInvoiceWorkflowActionsService
{
    /// <summary>
    /// Returns the transitions the acting user may execute right now for the
    /// given invoice's current status. Returns a <c>Invoice.NotFound</c> failure
    /// if the invoice does not exist (or is not visible to the acting tenant).
    /// </summary>
    Task<Result<IReadOnlyList<AvailableActionDto>>> GetAvailableActionsAsync(
        Guid invoiceId, CancellationToken cancellationToken = default);
}
