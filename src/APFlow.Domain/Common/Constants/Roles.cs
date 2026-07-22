namespace APFlow.Domain.Common.Constants;

/// <summary>
/// The fixed set of application roles used for role-based authorization, per the
/// canonical Role Catalogue in docs/06_Domain_Reference_Data.md §1 (SA-007 E-05) -
/// the single source of truth for these values, not WP-002's original best-effort
/// guess. Do not add, rename, or remove entries without explicit Architect
/// sign-off (see that document's AI Agent Rules).
/// These are the exact role names expected in the "roles" claim of a validated JWT
/// issued by Microsoft Entra External ID (App Roles), and the exact values passed
/// to <c>[Authorize(Roles = ...)]</c> or the named policies registered by
/// <c>AddApiAuthorization</c>.
/// VALUES USE SA-007 E-05's "Role Code" column (e.g. <c>"PLATFORM_ADMIN"</c>), not
/// its "Role Name" column (e.g. "Platform Administrator") - WP-046's own task
/// description lists the display names for readability, but the Role Code column
/// is what SA-007 E-05 defines as the actual catalogue value, and it matches
/// Entra's own convention that App Role "Value" fields avoid spaces - exactly the
/// risk WP-002-Entra-Verification-Checklist.md already flagged against the
/// previous (now-replaced) "AP Manager"/"AP Clerk" values. See
/// docs/WP-046-Role-Catalogue-Remediation.md.
/// IMPORTANT: these constants must still match the "Value" field configured for
/// each App Role in the real Entra App Registration exactly, character for
/// character - see WP-002-Entra-Verification-Checklist.md (updated by WP-046 to
/// reference these values). If the actual App Registration uses different values,
/// update these constants to match - do not change them independently of the App
/// Registration.
/// </summary>
public static class Roles
{
    /// <summary>Full platform administrative access.</summary>
    public const string PlatformAdmin = "PLATFORM_ADMIN";

    /// <summary>Reviews and processes AP invoices day to day.</summary>
    public const string ApReviewer = "AP_REVIEWER";

    /// <summary>Finance Manager / Decision-Maker - approval authority over financial commitments.</summary>
    public const string FinanceManager = "FINANCE_MANAGER";

    /// <summary>Credit control access, e.g. supplier account and payment-status oversight.</summary>
    public const string CreditController = "CREDIT_CONTROLLER";

    /// <summary>Accounts administration - day-to-day AP data entry and invoice processing.</summary>
    public const string AccountsAdmin = "ACCOUNTS_ADMIN";

    /// <summary>Read-only access with no ability to create, modify, or approve.</summary>
    public const string ReadOnly = "READ_ONLY";

    /// <summary>All defined roles, for validation and enumeration purposes.</summary>
    public static readonly IReadOnlyCollection<string> All =
    [
        PlatformAdmin,
        ApReviewer,
        FinanceManager,
        CreditController,
        AccountsAdmin,
        ReadOnly,
    ];
}
