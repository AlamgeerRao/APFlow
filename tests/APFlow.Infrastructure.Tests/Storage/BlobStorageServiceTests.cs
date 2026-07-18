using APFlow.Domain.Common;
using APFlow.Infrastructure.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace APFlow.Infrastructure.Tests.Storage;

public class BlobStorageServiceTests
{
    // --- Upload -------------------------------------------------------------

    [Fact]
    public async Task UploadAsync_Success_ReturnsUri()
    {
        var (service, _) = CreateService(FakeBlobContainerOperations.Behavior.Succeed);

        var result = await service.UploadAsync("invoice.pdf", new MemoryStream());

        Assert.True(result.IsSuccess);
        Assert.Contains("invoice.pdf", result.Value);
    }

    [Fact]
    public async Task UploadAsync_EmptyBlobName_ReturnsFailure_WithoutCallingOperations()
    {
        var (service, ops) = CreateService(FakeBlobContainerOperations.Behavior.ThrowGeneric);

        var result = await service.UploadAsync("", new MemoryStream());

        Assert.True(result.IsFailure);
        Assert.Equal("BlobStorage.InvalidBlobName", result.Error.Code);
        Assert.Null(ops.LastBlobName);
    }

    [Fact]
    public async Task UploadAsync_BlobNameTooLong_ReturnsFailure_WithoutCallingOperations()
    {
        var (service, ops) = CreateService(FakeBlobContainerOperations.Behavior.ThrowGeneric);
        var tooLongName = new string('a', 1025);

        var result = await service.UploadAsync(tooLongName, new MemoryStream());

        Assert.True(result.IsFailure);
        Assert.Equal("BlobStorage.InvalidBlobName", result.Error.Code);
        Assert.Null(ops.LastBlobName);
    }

    [Theory]
    [InlineData("invoice.")]
    [InlineData("folder/")]
    public async Task UploadAsync_BlobNameEndsWithDotOrSlash_ReturnsFailure_WithoutCallingOperations(string invalidName)
    {
        var (service, ops) = CreateService(FakeBlobContainerOperations.Behavior.ThrowGeneric);

        var result = await service.UploadAsync(invalidName, new MemoryStream());

        Assert.True(result.IsFailure);
        Assert.Equal("BlobStorage.InvalidBlobName", result.Error.Code);
        Assert.Null(ops.LastBlobName);
    }

    [Fact]
    public async Task UploadAsync_OperationsThrows_ReturnsFailure_DoesNotPropagate()
    {
        var (service, _) = CreateService(FakeBlobContainerOperations.Behavior.ThrowGeneric);

        var result = await service.UploadAsync("invoice.pdf", new MemoryStream());

        Assert.True(result.IsFailure);
        Assert.Equal("BlobStorage.UploadFailed", result.Error.Code);
    }

    [Fact]
    public async Task UploadAsync_CallerCancels_PropagatesCancellation()
    {
        var (service, _) = CreateService(FakeBlobContainerOperations.Behavior.ThrowOperationCanceled);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.UploadAsync("invoice.pdf", new MemoryStream(), cancellationToken: cts.Token));
    }

    [Theory]
    [InlineData("upload")]
    [InlineData("download")]
    [InlineData("delete")]
    [InlineData("sas")]
    [InlineData("verify")]
    public async Task AllOperations_CallerCancels_PropagateCancellation_NotJustUpload(string operation)
    {
        // WP-005 review: cancellation was only tested for UploadAsync even though all
        // five methods share the identical catch (OperationCanceledException) when
        // (...) { throw; } pattern. This proves it for every method, not just one.
        var (service, _) = CreateService(FakeBlobContainerOperations.Behavior.ThrowOperationCanceled);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Func<Task> act = operation switch
        {
            "upload" => () => service.UploadAsync("invoice.pdf", new MemoryStream(), cancellationToken: cts.Token),
            "download" => () => service.DownloadAsync("invoice.pdf", cts.Token),
            "delete" => () => service.DeleteAsync("invoice.pdf", cts.Token),
            "sas" => () => service.GenerateSasUrlAsync("invoice.pdf", TimeSpan.FromMinutes(15), cts.Token),
            "verify" => () => service.VerifyContainerAccessAsync(cts.Token),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(act);
    }

    // --- Download -----------------------------------------------------------

    [Fact]
    public async Task DownloadAsync_Success_ReturnsStream()
    {
        var (service, _) = CreateService(FakeBlobContainerOperations.Behavior.Succeed);

        var result = await service.DownloadAsync("invoice.pdf");

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Value);
    }

    [Fact]
    public async Task DownloadAsync_NotFound_ReturnsSpecificNotFoundError()
    {
        var (service, _) = CreateService(FakeBlobContainerOperations.Behavior.ThrowNotFound);

        var result = await service.DownloadAsync("missing.pdf");

        Assert.True(result.IsFailure);
        Assert.Equal("BlobStorage.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task DownloadAsync_GenericFailure_ReturnsGenericError()
    {
        var (service, _) = CreateService(FakeBlobContainerOperations.Behavior.ThrowGeneric);

        var result = await service.DownloadAsync("invoice.pdf");

        Assert.True(result.IsFailure);
        Assert.Equal("BlobStorage.DownloadFailed", result.Error.Code);
    }

    // --- Delete ---------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_Success_ReturnsSuccess()
    {
        var (service, _) = CreateService(FakeBlobContainerOperations.Behavior.Succeed);

        var result = await service.DeleteAsync("invoice.pdf");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DeleteAsync_Failure_ReturnsFailure()
    {
        var (service, _) = CreateService(FakeBlobContainerOperations.Behavior.ThrowGeneric);

        var result = await service.DeleteAsync("invoice.pdf");

        Assert.True(result.IsFailure);
        Assert.Equal("BlobStorage.DeleteFailed", result.Error.Code);
    }

    // --- SAS URL --------------------------------------------------------------

    [Fact]
    public async Task GenerateSasUrlAsync_Success_ReturnsUri()
    {
        var (service, _) = CreateService(FakeBlobContainerOperations.Behavior.Succeed);

        var result = await service.GenerateSasUrlAsync("invoice.pdf", TimeSpan.FromMinutes(15));

        Assert.True(result.IsSuccess);
        Assert.Contains("invoice.pdf", result.Value.ToString());
    }

    [Fact]
    public async Task GenerateSasUrlAsync_NonPositiveExpiry_ReturnsFailure_WithoutCallingOperations()
    {
        var (service, ops) = CreateService(FakeBlobContainerOperations.Behavior.ThrowGeneric);

        var result = await service.GenerateSasUrlAsync("invoice.pdf", TimeSpan.Zero);

        Assert.True(result.IsFailure);
        Assert.Equal("BlobStorage.InvalidExpiry", result.Error.Code);
        Assert.Null(ops.LastBlobName);
    }

    // --- Verify container access (health check path) --------------------------

    [Fact]
    public async Task VerifyContainerAccessAsync_Success_ReturnsTrue()
    {
        var (service, ops) = CreateService(FakeBlobContainerOperations.Behavior.Succeed);
        ops.ExistsResult = true;

        Assert.True(await service.VerifyContainerAccessAsync());
    }

    [Fact]
    public async Task VerifyContainerAccessAsync_ContainerDoesNotExist_ReturnsFalse()
    {
        var (service, ops) = CreateService(FakeBlobContainerOperations.Behavior.Succeed);
        ops.ExistsResult = false;

        Assert.False(await service.VerifyContainerAccessAsync());
    }

    [Fact]
    public async Task VerifyContainerAccessAsync_Throws_ReturnsFalse_DoesNotPropagate()
    {
        var (service, _) = CreateService(FakeBlobContainerOperations.Behavior.ThrowGeneric);

        Assert.False(await service.VerifyContainerAccessAsync());
    }

    // --- Tenant scoping (docs/WP-005-Blob-Storage-Tenant-Isolation-Decision.md) -----

    [Fact]
    public async Task UploadAsync_PrefixesPhysicalBlobNameWithCallerTenantId()
    {
        var (service, ops) = CreateService(FakeBlobContainerOperations.Behavior.Succeed, tenantId: "tenant-a");

        await service.UploadAsync("invoice.pdf", new MemoryStream());

        Assert.Equal("tenant-a/invoice.pdf", ops.LastBlobName);
    }

    [Fact]
    public async Task UploadAsync_DifferentTenants_ProduceDifferentPhysicalBlobNames_ForSameLogicalName()
    {
        // Proves the isolation property directly: two different tenant contexts
        // requesting the identical logical blob name must never resolve to the same
        // physical blob - same "prove non-leakage across tenants" bar WP-003's
        // decision doc set for the EF Core query filter.
        var (serviceA, opsA) = CreateService(FakeBlobContainerOperations.Behavior.Succeed, tenantId: "tenant-a");
        var (serviceB, opsB) = CreateService(FakeBlobContainerOperations.Behavior.Succeed, tenantId: "tenant-b");

        await serviceA.UploadAsync("invoice.pdf", new MemoryStream());
        await serviceB.UploadAsync("invoice.pdf", new MemoryStream());

        Assert.NotEqual(opsA.LastBlobName, opsB.LastBlobName);
        Assert.Equal("tenant-a/invoice.pdf", opsA.LastBlobName);
        Assert.Equal("tenant-b/invoice.pdf", opsB.LastBlobName);
    }

    [Theory]
    [InlineData("upload")]
    [InlineData("download")]
    [InlineData("delete")]
    [InlineData("sas")]
    public async Task AllBlobNameOperations_NoTenantContext_ReturnFailure_WithoutCallingOperations(string operation)
    {
        var (service, ops) = CreateService(FakeBlobContainerOperations.Behavior.ThrowGeneric, tenantId: null);

        Func<Task<Result>> act = operation switch
        {
            "upload" => async () => await service.UploadAsync("invoice.pdf", new MemoryStream()),
            "download" => async () => await service.DownloadAsync("invoice.pdf"),
            "delete" => () => service.DeleteAsync("invoice.pdf"),
            "sas" => async () => await service.GenerateSasUrlAsync("invoice.pdf", TimeSpan.FromMinutes(15)),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };

        var result = await act();

        Assert.True(result.IsFailure);
        Assert.Equal("BlobStorage.NoTenantContext", result.Error.Code);
        Assert.Null(ops.LastBlobName);
    }

    private static (BlobStorageService Service, FakeBlobContainerOperations Operations) CreateService(
        FakeBlobContainerOperations.Behavior behavior, string? tenantId = "tenant-a")
    {
        var operations = new FakeBlobContainerOperations { Mode = behavior };
        var currentUser = new FakeCurrentUserService { TenantId = tenantId };
        var service = new BlobStorageService(operations, currentUser, NullLogger<BlobStorageService>.Instance);
        return (service, operations);
    }
}
