namespace APFlow.Infrastructure.Configuration;

/// <summary>
/// Binds the "Workers" configuration section for the one setting Infrastructure
/// itself needs from it: which AP Flow tenant a background worker (no HTTP
/// request, no <c>HttpContext</c>) acts on behalf of. Deliberately a separate,
/// narrowly-scoped class from <c>APFlow.Workers.WorkersOptions</c> (which binds
/// the same section for its own, unrelated "EmailPollingIntervalSeconds" setting)
/// rather than adding a project reference between the two layers just to share
/// one class - see WP-069.
/// </summary>
public sealed class WorkerIdentityOptions
{
    /// <summary>The configuration section name this class binds to.</summary>
    public const string SectionName = "Workers";

    /// <summary>
    /// The AP Flow tenant id (matches a real signed-in user's Entra "tid" claim -
    /// see <see cref="APFlow.Application.Interfaces.ICurrentUserService.TenantId"/>)
    /// that background workers operate on behalf of. Must be the SAME value used
    /// by real GB Skips users signing in (currently the CIAM sign-in tenant id,
    /// not <c>WorkflowSeedData.GbSkipsPlaceholderTenantId</c>, which is explicitly
    /// documented as never matching any real caller) - using the wrong value here
    /// would make worker-created rows invisible to every real user.
    /// </summary>
    public string? TenantId { get; init; }
}
