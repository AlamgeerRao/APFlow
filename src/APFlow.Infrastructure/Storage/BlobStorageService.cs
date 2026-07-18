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
/// </summary>
public sealed class BlobStorageService : IBlobStorageService
{
    private readonly IBlobContainerOperations _operations;
    private readonly ILogger<BlobStorageService> _logger;

    /// <summary>Creates a new <see cref="BlobStorageService"/>.</summary>
    internal BlobStorageService(IBlobContainerOperations operations, ILogger<BlobStorageService> logger)
    {
        _operations = operations;
        _logger = logger;
    }

    // Basic Azure blob naming constraints - not exhaustive, but catches the common
    // malformed-input cases with a clear Result.Failure/error code rather than
    // letting them surface as an opaque SDK exception. See Microsoft's blob/container
    // naming rules for the full constraint set if stricter validation is ever needed.
    private const int MaxBlobNameLength = 1024;

    private static Error? ValidateBlobName(string blobName)
    {
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return new Error("BlobStorage.InvalidBlobName", "Blob name must not be empty.");
        }

        if (blobName.Length > MaxBlobNameLength)
        {
            return new Error("BlobStorage.InvalidBlobName", $"Blob name must not exceed {MaxBlobNameLength} characters.");
        }

        if (blobName.EndsWith('.') || blobName.EndsWith('/'))
        {
            return new Error("BlobStorage.InvalidBlobName", "Blob name must not end with '.' or '/'.");
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<Result<string>> UploadAsync(string blobName, Stream content, string? contentType = null, CancellationToken cancellationToken = default)
    {
        var uploadValidationError = ValidateBlobName(blobName);
        if (uploadValidationError is not null)
        {
            return Result.Failure<string>(uploadValidationError);
        }

        try
        {
            var uri = await _operations.UploadAsync(blobName, content, contentType, cancellationToken);
            return Result.Success(uri);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload blob {BlobName}.", blobName);
            return Result.Failure<string>(new Error("BlobStorage.UploadFailed", $"Failed to upload blob '{blobName}'."));
        }
    }

    /// <inheritdoc />
    public async Task<Result<Stream>> DownloadAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var downloadValidationError = ValidateBlobName(blobName);
        if (downloadValidationError is not null)
        {
            return Result.Failure<Stream>(downloadValidationError);
        }

        try
        {
            var stream = await _operations.DownloadAsync(blobName, cancellationToken);
            return Result.Success(stream);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("Blob {BlobName} was not found.", blobName);
            return Result.Failure<Stream>(new Error("BlobStorage.NotFound", $"Blob '{blobName}' was not found."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download blob {BlobName}.", blobName);
            return Result.Failure<Stream>(new Error("BlobStorage.DownloadFailed", $"Failed to download blob '{blobName}'."));
        }
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(string blobName, CancellationToken cancellationToken = default)
    {
        var deleteValidationError = ValidateBlobName(blobName);
        if (deleteValidationError is not null)
        {
            return Result.Failure(deleteValidationError);
        }

        try
        {
            await _operations.DeleteAsync(blobName, cancellationToken);
            return Result.Success();
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete blob {BlobName}.", blobName);
            return Result.Failure(new Error("BlobStorage.DeleteFailed", $"Failed to delete blob '{blobName}'."));
        }
    }

    /// <inheritdoc />
    public async Task<Result<Uri>> GenerateSasUrlAsync(string blobName, TimeSpan validFor, CancellationToken cancellationToken = default)
    {
        var sasValidationError = ValidateBlobName(blobName);
        if (sasValidationError is not null)
        {
            return Result.Failure<Uri>(sasValidationError);
        }

        if (validFor <= TimeSpan.Zero)
        {
            return Result.Failure<Uri>(new Error("BlobStorage.InvalidExpiry", "SAS validity duration must be positive."));
        }

        try
        {
            var uri = await _operations.GenerateSasUriAsync(blobName, validFor, cancellationToken);
            return Result.Success(uri);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate SAS URL for blob {BlobName}.", blobName);
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
