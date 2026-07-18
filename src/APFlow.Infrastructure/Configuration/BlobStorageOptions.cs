namespace APFlow.Infrastructure.Configuration;

/// <summary>
/// Binds the "BlobStorage" configuration section. Supports two auth paths, same
/// pattern as Graph (WP-004): Managed Identity (preferred, no secret) via
/// <see cref="AccountUrl"/>, or a connection string (account key) fallback.
/// A single configured container is used for everything at this stage - WP-005 is
/// generic storage plumbing, not tied to any particular document type. Splitting by
/// container (e.g. per document type) is a decision for whichever future work
/// package has real requirements for it, not invented here.
/// </summary>
public sealed class BlobStorageOptions
{
    /// <summary>The configuration section name this class binds to.</summary>
    public const string SectionName = "BlobStorage";

    /// <summary>
    /// The storage account's blob endpoint, e.g. "https://apflowstorage.blob.core.windows.net/".
    /// Used with Managed Identity (<c>DefaultAzureCredential</c>) when
    /// <see cref="ConnectionString"/> is blank - the preferred, secret-less path.
    /// </summary>
    public string AccountUrl { get; init; } = string.Empty;

    /// <summary>
    /// OPTIONAL account-key connection string fallback. If set, used instead of
    /// <see cref="AccountUrl"/> + Managed Identity. Must come from Key Vault - never a
    /// literal value in appsettings. Using this path means SAS generation uses direct
    /// account-key signing rather than a User Delegation SAS - see
    /// <see cref="APFlow.Application.Interfaces.IBlobStorageService.GenerateSasUrlAsync"/>.
    /// </summary>
    public string ConnectionString { get; init; } = string.Empty;

    /// <summary>The blob container this service operates against.</summary>
    public string ContainerName { get; init; } = string.Empty;
}
