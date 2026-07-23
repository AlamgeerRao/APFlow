using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Validates a status transition against the acting tenant's active
/// <c>WorkflowTemplate</c> (WP-050 task 5) - data-driven, not a hardcoded
/// enum-based switch. Built and fully unit-tested against arbitrary,
/// test-constructed transition data (see <c>WorkflowValidationServiceTests</c>),
/// proving the MECHANISM works correctly independent of which specific real-world
/// transitions end up being confirmed.
/// NOT YET CALLED from <c>InvoiceService.UpdateAsync</c>'s blocking path - see
/// docs/WP-050-Workflow-Engine-Decisions.md. Enforcing this would require a
/// confirmed transition rule set for BOTH the platform-default template (whose
/// transition graph is not documented anywhere in this project's reference
/// material - only its status LIST is) and GB Skips' proposed additions (task 4's
/// own wording explicitly prohibits finalising these without Chief Technical
/// Architect sign-off). With zero seeded <c>WorkflowTransition</c> rows for either
/// template today, wiring this in as a blocking gate would reject every status
/// change in the application, not just GB Skips' two new ones - a regression, not
/// an enforcement of anything confirmed. This service is ready to be wired in the
/// moment both transition sets are confirmed and seeded.
/// </summary>
public interface IWorkflowValidationService
{
    /// <summary>
    /// Returns a failure if <paramref name="fromStatusCode"/> -&gt;
    /// <paramref name="toStatusCode"/> is not an allowed transition for the current
    /// tenant's active <c>WorkflowTemplate</c> for <paramref name="domainName"/>,
    /// or if either status code is not even valid for that template. Returns
    /// success (no value) if the transition is allowed.
    /// </summary>
    Task<Result> ValidateTransitionAsync(
        string domainName, string fromStatusCode, string toStatusCode, CancellationToken cancellationToken = default);
}
