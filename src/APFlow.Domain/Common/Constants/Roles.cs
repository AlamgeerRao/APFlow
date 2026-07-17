namespace APFlow.Domain.Common.Constants;

/// <summary>
/// The fixed set of application roles used for role-based authorization, as defined
/// in WP-002. These are the exact role names expected in the "roles" claim of a
/// validated JWT issued by Microsoft Entra External ID (App Roles), and the exact
/// values passed to <c>[Authorize(Roles = ...)]</c> or the named policies registered
/// by <c>AddApiAuthorization</c>.
/// IMPORTANT: these constants must match the "Value" field configured for each App
/// Role in the Entra App Registration exactly, character for character. Entra App
/// Role values conventionally avoid spaces (e.g. "ApManager" rather than "AP Manager").
/// The values below use the display names as given in the WP-002 work package; if the
/// actual App Registration uses different values, update these constants to match -
/// do not change them independently of the App Registration.
/// </summary>
public static class Roles
{
    /// <summary>Full administrative access.</summary>
    public const string Administrator = "Administrator";

    /// <summary>Manages the AP workflow: approvals and oversight of AP processing.</summary>
    public const string ApManager = "AP Manager";

    /// <summary>Performs day-to-day AP data entry and invoice processing.</summary>
    public const string ApClerk = "AP Clerk";

    /// <summary>Finance team access, e.g. for reporting and reconciliation.</summary>
    public const string Finance = "Finance";

    /// <summary>Read-only access with no ability to create, modify, or approve.</summary>
    public const string ReadOnly = "ReadOnly";

    /// <summary>All defined roles, for validation and enumeration purposes.</summary>
    public static readonly IReadOnlyCollection<string> All =
    [
        Administrator,
        ApManager,
        ApClerk,
        Finance,
        ReadOnly,
    ];
}
