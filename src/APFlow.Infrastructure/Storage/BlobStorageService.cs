using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using Azure;
using Microsoft.Extensions.Logging;

namespace APFlow.Infrastructure.Storage;

/// <summary>
/// Azure Blob Storage-backed implementation of <see cref="IBlobStorageService"/>.
/// Depends on <see cref="IBlobContainerOperations"/> rather than the Azure SDK
/// directly - see that interface's doc comment for why (testability of the paths
/// below without needing to fake the SDK's internals).
/// TENANT ISOLATION (see docs/WP-005-Blob-Storage-Tenant-Isolation-Decision.md):
/// every caller-supplied blob name is transparently prefixed with the current
/// caller's tenant id before it ever reaches Azure Storage. This is enforced here,
/// not left to callers, so no future caller can forget it or bypass it - same
/// "enforce centrally" pattern as <c>AppDbContext</c> stamping <c>TenantId</c> on
/// write. Registered Scoped, not Singleton, specifically because of this: a
/// Singleton would capture <see cref="ICurrentUserService"/> from whichever
/// request happened to construct it first and freeze to that tenant forever - the
/// same bug class flagged in docs/WP-003-Tenant-Isolation-Decision.md for the EF
/// Core query filter.
/// </summary>
public sealed class BlobStorageService : IBlobStorageService
{
    private readonly IBlobContainerOperations _operations;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<BlobStorageService> _logger;

    /// <summary>Creates a new <see cref="BlobStorageService"/>.</summary>
    internal BlobStorageService(IBlobContainerOperations operations, ICurrentUserService currentUserService, ILogger<BlobStorageService> logger)
    {
        _operations = operations;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    // Basic Azure blob naming constraints - not exhaustive, but catches the common
    // malformed-input cases with a clear Result.Failure/error code rather than
    // letting them surface as an opaque SDK exception. See Microsoft's blob/container
    // naming rules for the full constraint set if stricter validation is ever needed.
    private const int MaxPhysicalBlobNameLength = 1024;

    // Reserves room in the 1024-char Azure limit for the "{tenantId}/" prefix added
    // by ScopeBlobName below. Entra "tid" claims are GUIDs (36 chars); 40 leaves
    // headroom without being so generous it meaningfully shrinks the caller's budget.
    private const int TenantPrefixReservedLength = 40;
    private const int MaxCallerBlobNameLength = MaxPhysicalBlobNameLength - TenantPrefixReservedLength;

    private static Error? ValidateBlobName(string blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return new Error("BlobStorage.InvalidBlobName", "Blob name must not be empty.");
        }

        if (blobName.Length > MaxCallerBlobNameLength)
        {
            return new Error("BlobStorage.InvalidBlobName", $"Blob name must not exceed {MaxCallerBlobNameLength} characters.");
        }

        if (blobName.EndsWith('.') || blobName.EndsWith('/'))
        {
            return new Error("BlobStorage.InvalidBlobName", "Blob name must not end with '.' or '/'.");
        }

        return null;
    }

    /// <summary>
    /// Validates <paramref name="blobName"/> and prefixes it with the current caller's
    /// tenant id, producing the physical name actually stored in Blob Storage. Fails
    /// with <c>BlobStorage.NoTenantContext</c> rather than falling back to an
    /// un-scoped name if there is no current tenant - silently writing to a shared,
    /// un-prefixed path would defeat the entire point of this method.
    /// </summary>
    private Result<string> ScopeBlobName(string blobName)
    {
        var validationError = ValidateBlobName(blobName);
        if (validationError is not null)
        {
            return Result.Failure<string>(validationError);
        }

        var tenantId = _currentUserService.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return Result.Failure<string>(new Error(
                "BlobStorage.NoTenantContext",
                "Blob Storage operations require an authenticated caller with a tenant id."));
        }

        return Result.Success($"{tenantId}/{blobName}");
    }

    /// <inheritdoc />
    public async Task<Result<string>> UploadAsync(string blobName, Stream content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var scopeResult = ScopeBlobName(blobName);
        if (scopeResult.IsFailure)
        {
            return Result.Failure<string>(scopeResult.Error);
        }

        try
        {
            var uri = await _operations.UploadAsync(scopeResult.Value, content, contentType, cancellationToken);
            return Result.Success(uri);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload blob {BlobName}.", scopeResult.Value);
            return Result.Failure<string>(new Error("BlobStorage.UploadFailed", $"Failed to upload blob '{blobName}'."));
        }
    }

    /// <inheritdoc />
    public async Task<Result<Stream>> DownloadAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var scopeResult = ScopeBlobName(blobName);
        if (scopeResult.IsFailure)
        {
            return Result.Failure<Stream>(scopeResult.Error);
        }

        try
        {
            var stream = await _operations.DownloadAsync(scopeResult.Value, cancellationToken);
            return Result.Success(stream);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Blob {BlobName} was not found.", scopeResult.Value);
            return Result.Failure<Stream>(new Error("BlobStorage.NotFound", $"Blob '{blobName}' was not found."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download blob {BlobName}.", scopeResult.Value);
            return Result.Failure<Stream>(new Error("BlobStorage.DownloadFailed", $"Failed to download blob '{blobName}'."));
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var scopeResult = ScopeBlobName(blobName);
        if (scopeResult.IsFailure)
        {
            return Result.Failure(scopeResult.Error);
        }

        try
        {
            await _operations.DeleteAsync(scopeResult.Value, cancellationToken);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete blob {BlobName}.", scopeResult.Value);
            return Result.Failure(new Error("BlobStorage.DeleteFailed", $"Failed to delete blob '{blobName}'."));
        }
    }

    /// <inheritdoc />
    public async Task<Result<Uri>> GenerateSasUrlAsync(string blobName, TimeSpan validFor, CancellationToken cancellationToken = default)
    {
        var scopeResult = ScopeBlobName(blobName);
        if (scopeResult.IsFailure)
        {
            return Result.Failure<Uri>(scopeResult.Error);
        }

        if (validFor <= TimeSpan.Zero)
        {
            return Result.Failure<Uri>(new Error("BlobStorage.InvalidExpiry", "SAS validity duration must be positive."));
        }

        try
        {
            var uri = await _operations.GenerateSasUriAsync(scopeResult.Value, validFor, cancellationToken);
            return Result.Success(uri);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate SAS URL for blob {BlobName}.", scopeResult.Value);
            return Result.Failure<Uri>(new Error("BlobStorage.SasGenerationFailed", $"Failed to generate a SAS URL for blob '{blobName}'."));
        }
    }

    /// <inheritdoc />
    public async Task<bool> VerifyContainerAccessAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _operations.ExistsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Blob Storage container access verification failed.");
            return false;
        }
    }
}
