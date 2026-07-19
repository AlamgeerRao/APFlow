namespace APFlow.Integrations.DocumentIntelligence;

/// <summary>
/// Binds the "DocumentIntelligence" configuration section. Same auth pattern as
/// Graph (WP-004) and Blob Storage (WP-005): Managed Identity (preferred, no secret)
/// via <see cref="Endpoint"/>, or an API key fallback.
/// </summary>
public sealed class DocumentIntelligenceOptions
{
    /// <summary>The configuration section name this class binds to.</summary>
    public const string SectionName = "DocumentIntelligence";

    /// <summary>
    /// The Document Intelligence resource endpoint, e.g.
    /// "https://apflow-docintel.cognitiveservices.azure.com/".
    /// </summary>
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>
    /// OPTIONAL API key fallback. If blank, authentication uses
    /// <c>DefaultAzureCredential</c> (Managed Identity) instead - the preferred,
    /// secret-less path. If set, must come from Key Vault - never a literal value in
    /// appsettings.
    /// </summary>
    public string ApiKey { get; init; } = string.Empty;
}
