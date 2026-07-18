using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Infrastructure.Storage;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Xunit;

namespace APFlow.Infrastructure.Tests.Storage;

public class BlobStorageHealthCheckTests
{
    [Fact]
    public async Task CheckHealthAsync_ContainerAccessible_ReturnsHealthy()
    {
        var healthCheck = new BlobStorageHealthCheck(new FakeBlobStorageService(isAccessible: true));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    [Fact]
    public async Task CheckHealthAsync_ContainerNotAccessible_ReturnsDegraded_NotUnhealthy()
    {
        // Degraded, not Unhealthy, is deliberate - same reasoning as
        // GraphMailboxHealthCheckTests (WP-004): see
        // docs/WP-004-Health-Check-Severity-Decision.md, which now covers this check too.
        var healthCheck = new BlobStorageHealthCheck(new FakeBlobStorageService(isAccessible: false));

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        private readonly bool _isAccessible;

        public FakeBlobStorageService(bool isAccessible)
        {
            _isAccessible = isAccessible;
        }

        public Task<Result<string>> UploadAsync(string blobName, Stream content, string? contentType = null, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<Stream>> DownloadAsync(string blobName, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result> DeleteAsync(string blobName, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<Result<Uri>> GenerateSasUrlAsync(string blobName, TimeSpan validFor, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();

        public Task<bool> VerifyContainerAccessAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_isAccessible);
    }
}
