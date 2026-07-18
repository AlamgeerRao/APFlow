using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using APFlow.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace APFlow.Infrastructure.Storage;

/// <summary>
/// Thin seam between <see cref="BlobStorageService"/> and the Azure Storage SDK.
/// Exists specifically for testability, same reasoning as
/// APFlow.Integrations.Graph.IGraphInboxReader (WP-004): this project cannot reliably
/// fake Azure.Storage.Blobs' client types without a real package to verify against,
/// so this one interface is fully hand-written and owned here instead, letting
/// BlobStorageServiceTests fake it directly with full confidence it compiles and
/// behaves as intended. Internal: not part of the Application-facing abstraction
/// (<see cref="APFlow.Application.Interfaces.IBlobStorageService"/>) - only
/// BlobStorageService and its tests should depend on this.
/// </summary>
internal interface IBlobContainerOperations
{
    Task<string> UploadAsync(string blobName, Stream content, string? contentType, CancellationToken cancellationToken);

    Task<Stream> DownloadAsync(string blobName, CancellationToken cancellationToken);

    Task DeleteAsync(string blobName, CancellationToken cancellationToken);

    Task<Uri> GenerateSasUriAsync(string blobName, TimeSpan validFor, CancellationToken cancellationToken);

    Task<bool> ExistsAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Real implementation of <see cref="IBlobContainerOperations"/>, a direct wrapper
/// around Azure.Storage.Blobs. All logic worth unit-testing (error mapping, Result
/// wrapping, cancellation handling) lives in <see cref="BlobStorageService"/>, which
/// depends on the interface, not this class - this class is thin by design.
/// NOT independently unit-tested in this work package: doing so would require either
/// a real Azure Storage account/Azurite emulator, or faking Azure SDK client types
/// this project cannot verify offline. Deliberately kept
/// as small and literal a wrapper as possible to minimize what that gap leaves
/// unverified.
/// </summary>
internal sealed class BlobContainerOperations : IBlobContainerOperations
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly BlobStorageOptions _options;

    public BlobContainerOperations(BlobServiceClient blobServiceClient, IOptions<BlobStorageOptions> options)
    {
        _blobServiceClient = blobServiceClient;
        _options = options.Value;
    }

    private BlobContainerClient Container => _blobServiceClient.GetBlobContainerClient(_options.ContainerName);

    public async Task<string> UploadAsync(string blobName, Stream content, string? contentType, CancellationToken cancellationToken)
    {
        var blobClient = Container.GetBlobClient(blobName);
        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = string.IsNullOrWhiteSpace(contentType) ? null : new BlobHttpHeaders { ContentType = contentType },
        };

        await blobClient.UploadAsync(content, uploadOptions, cancellationToken);
        return blobClient.Uri.ToString();
    }

    public async Task<Stream> DownloadAsync(string blobName, CancellationToken cancellationToken)
    {
        var blobClient = Container.GetBlobClient(blobName);
        var result = await blobClient.DownloadStreamingAsync(cancellationToken: cancellationToken);
        return result.Value.Content;
    }

    public async Task DeleteAsync(string blobName, CancellationToken cancellationToken)
    {
        var blobClient = Container.GetBlobClient(blobName);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    public async Task<Uri> GenerateSasUriAsync(string blobName, TimeSpan validFor, CancellationToken cancellationToken)
    {
        var blobClient = Container.GetBlobClient(blobName);
        var expiresOn = DateTimeOffset.UtcNow.Add(validFor);

        if (blobClient.CanGenerateSasUri)
        {
            // Account-key/connection-string auth (BlobStorageOptions.ConnectionString
            // set): the client can sign a SAS directly with the account key.
            return blobClient.GenerateSasUri(BlobSasPermissions.Read, expiresOn);
        }

        // Managed Identity / AAD auth: no account key available on this client, so a
        // User Delegation SAS is used instead - signed by a short-lived key obtained
        // via Entra ID rather than the storage account key. Secretless, consistent
        // with the Key Vault (WP-001) / Graph (WP-004) Managed Identity pattern.
        var delegationKeyExpiresOn = DateTimeOffset.UtcNow.AddHours(1);
        var userDelegationKey = await _blobServiceClient.GetUserDelegationKeyAsync(
            DateTimeOffset.UtcNow, delegationKeyExpiresOn, cancellationToken);

        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = Container.Name,
            BlobName = blobName,
            Resource = "b",
            ExpiresOn = expiresOn,
        };
        sasBuilder.SetPermissions(BlobSasPermissions.Read);

        var sasQueryParameters = sasBuilder.ToSasQueryParameters(userDelegationKey.Value, _blobServiceClient.AccountName);

        var uriBuilder = new BlobUriBuilder(blobClient.Uri) { Sas = sasQueryParameters };
        return uriBuilder.ToUri();
    }

    public async Task<bool> ExistsAsync(CancellationToken cancellationToken)
    {
        Response<bool> response = await Container.ExistsAsync(cancellationToken);
        return response.Value;
    }
}
