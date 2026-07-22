using APFlow.Application.Features.Invoices;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Decides whether a caller is authorized to dismiss/override a duplicate-invoice
/// warning (the advisory flag <see cref="IDuplicateDetectionService"/> produces).
/// WP-047 requires this to be a single, named, swappable check - not an inline role-
/// string comparison scattered across whatever future caller ends up dismissing a
/// warning - specifically because the current answer (FINANCE_MANAGER only) is
/// provisional: it is the interim Full/Approver mapping from
/// docs/AI/06_Domain_Reference_Data.md §1, pending the Product Owner's follow-up
/// confirmation with GB Skips (see docs/AI/06_Domain_Reference_Data.md's own "interim,
/// pending WP-051 confirmation" note, and
/// docs/WP-047-Duplicate-Matching-Reconciliation.md). If that confirmation changes
/// which role(s) may override, only <see cref="DuplicateOverrideAuthorizationService"/>
/// needs to change - no calling code should ever compare a role string directly.
/// No caller in this codebase invokes this yet: there is no dismiss/override
/// endpoint or workflow built anywhere (approval workflow remains explicit
/// out-of-scope everywhere it has come up - WP-009, WP-010, WP-012). This interface
/// exists so that capability is ready, isolated, and correct the moment a future
/// work package builds the endpoint that needs it.
/// </summary>
public interface IDuplicateOverrideAuthorizationService
{
    /// <summary>
    /// Whether a caller holding the given roles (see <see cref="ICurrentUserService.Roles"/>)
    /// may dismiss/override a duplicate-invoice warning.
    /// </summary>
    bool CanOverrideDuplicateWarning(IReadOnlyCollection<string> roles);
}
