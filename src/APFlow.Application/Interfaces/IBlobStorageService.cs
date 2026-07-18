using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Abstraction over blob storage, backed by Azure Blob Storage (see
/// APFlow.Infrastructure.Storage.BlobStorageService). Generic by design - blob name,
/// stream, content type only. WP-005 scope: not wired into any invoice/document
/// workflow - that wiring is explicitly deferred to whichever future work package
/// needs it.
/// </summary>
public interface IBlobStorageService
{
    /// <summary>Uploads content to the given blob name in the configured container. Returns the blob's URI on success.</summary>
    Task<Result<string>> UploadAsync(string blobName, Stream content, string? contentType = null, CancellationToken cancellationToken = default);

    /// <summary>Downloads the given blob's content as a stream. Caller is responsible for disposing the stream.</summary>
    Task<Result<Stream>> DownloadAsync(string blobName, CancellationToken cancellationToken = default);

    /// <summary>Deletes the given blob if it exists. Succeeds (no-op) if the blob does not exist.</summary>
    Task<Result> DeleteAsync(string blobName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a time-limited, read-only SAS URL for the given blob. Uses a User
    /// Delegation SAS (Entra ID-signed, no storage account key involved) when the
    /// service is configured with Managed Identity; falls back to account-key-based
    /// SAS when configured with a connection string.
    /// </summary>
    Task<Result<Uri>> GenerateSasUrlAsync(string blobName, TimeSpan validFor, CancellationToken cancellationToken = default);

    /// <summary>
    /// Verifies the configured container is reachable with the configured
    /// credentials, without reading or listing any blob content. Returns false
    /// rather than throwing on failure - this is a health/diagnostic check.
    /// </summary>
    Task<bool> VerifyContainerAccessAsync(CancellationToken cancellationToken = default);
}
