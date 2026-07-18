using APFlow.Infrastructure.Storage;
using Azure;

namespace APFlow.Infrastructure.Tests.Storage;

/// <summary>
/// Hand-written fake for <see cref="IBlobContainerOperations"/>, fully controlled by
/// this test project (no Azure SDK types faked) - same reasoning as WP-004's
/// FakeGraphInboxReader.
/// </summary>
internal sealed class FakeBlobContainerOperations : IBlobContainerOperations
{
    public enum Behavior
    {
        Succeed,
        ThrowGeneric,
        ThrowNotFound,
        ThrowOperationCanceled,
    }

    public Behavior Mode { get; set; } = Behavior.Succeed;
    public string? LastBlobName { get; private set; }
    public bool ExistsResult { get; set; } = true;

    public Task<string> UploadAsync(string blobName, Stream content, string? contentType, CancellationToken cancellationToken)
    {
        LastBlobName = blobName;
        return Mode switch
        {
            Behavior.Succeed => Task.FromResult($"https://fake.blob.core.windows.net/container/{blobName}"),
            Behavior.ThrowOperationCanceled => throw new OperationCanceledException(cancellationToken),
            _ => throw new InvalidOperationException("Simulated upload failure."),
        };
    }

    public Task<Stream> DownloadAsync(string blobName, CancellationToken cancellationToken)
    {
        LastBlobName = blobName;
        return Mode switch
        {
            Behavior.Succeed => Task.FromResult<Stream>(new MemoryStream([1, 2, 3])),
            Behavior.ThrowNotFound => throw new RequestFailedException(404, "Blob not found."),
            Behavior.ThrowOperationCanceled => throw new OperationCanceledException(cancellationToken),
            _ => throw new InvalidOperationException("Simulated download failure."),
        };
    }

    public Task DeleteAsync(string blobName, CancellationToken cancellationToken)
    {
        LastBlobName = blobName;
        return Mode switch
        {
            Behavior.Succeed => Task.CompletedTask,
            Behavior.ThrowOperationCanceled => throw new OperationCanceledException(cancellationToken),
            _ => throw new InvalidOperationException("Simulated delete failure."),
        };
    }

    public Task<Uri> GenerateSasUriAsync(string blobName, TimeSpan validFor, CancellationToken cancellationToken)
    {
        LastBlobName = blobName;
        return Mode switch
        {
            Behavior.Succeed => Task.FromResult(new Uri($"https://fake.blob.core.windows.net/container/{blobName}?sig=fake")),
            Behavior.ThrowOperationCanceled => throw new OperationCanceledException(cancellationToken),
            _ => throw new InvalidOperationException("Simulated SAS generation failure."),
        };
    }

    public Task<bool> ExistsAsync(CancellationToken cancellationToken) => Mode switch
    {
        Behavior.Succeed => Task.FromResult(ExistsResult),
        Behavior.ThrowOperationCanceled => throw new OperationCanceledException(cancellationToken),
        _ => throw new InvalidOperationException("Simulated existence check failure."),
    };
}
