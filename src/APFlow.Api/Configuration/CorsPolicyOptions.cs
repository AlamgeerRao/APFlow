namespace APFlow.Api.Configuration;

/// <summary>
/// Binds the "Cors" configuration section (WP-059 Part B). See
/// <c>ApiServiceCollectionExtensions.AddApiServices</c> for how an empty
/// <see cref="AllowedOrigins"/> list is interpreted (permissive fallback in
/// Development only; fails closed - no origins allowed - in any other
/// environment).
/// </summary>
public sealed class CorsPolicyOptions
{
    /// <summary>The configuration section name this class binds to.</summary>
    public const string SectionName = "Cors";

    /// <summary>
    /// The exact origins (scheme + host + port, no path, no trailing slash) allowed
    /// to make cross-origin requests to this API. Empty by default - see this
    /// class's own doc comment for how an empty list is interpreted per
    /// environment.
    /// </summary>
    public string[] AllowedOrigins { get; init; } = [];
}
