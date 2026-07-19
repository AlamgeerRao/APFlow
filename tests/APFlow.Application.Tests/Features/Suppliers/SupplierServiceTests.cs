using APFlow.Application.DTOs;
using APFlow.Application.Features.Suppliers;
using APFlow.Application.Tests.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace APFlow.Application.Tests.Features.Suppliers;

public class SupplierServiceTests
{
    [Fact]
    public async Task CreateAsync_ValidName_Succeeds()
    {
        var (service, repo) = CreateService();

        var result = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Acme Ltd", result.Value.Name);
        Assert.Single(repo.Suppliers);
    }

    [Fact]
    public async Task CreateAsync_EmptyName_ReturnsFailure()
    {
        var (service, _) = CreateService();

        var result = await service.CreateAsync(new SaveSupplierRequest(""));

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.InvalidName", result.Error.Code);
    }

    [Fact]
    public async Task GetByIdAsync_MissingSupplier_ReturnsFailure()
    {
        var (service, _) = CreateService();

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_ExistingSupplier_UpdatesName()
    {
        var (service, repo) = CreateService();
        var created = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd"));

        var result = await service.UpdateAsync(created.Value.Id, new SaveSupplierRequest("Acme Renamed"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Acme Renamed", result.Value.Name);
    }

    [Fact]
    public async Task UpdateAsync_EmptyName_ReturnsFailure()
    {
        var (service, repo) = CreateService();
        var created = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd"));

        var result = await service.UpdateAsync(created.Value.Id, new SaveSupplierRequest(""));

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.InvalidName", result.Error.Code);
    }

    [Fact]
    public async Task DeleteAsync_ExistingSupplier_RemovesFromRepository()
    {
        var (service, repo) = CreateService();
        var created = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd"));

        var result = await service.DeleteAsync(created.Value.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(repo.Suppliers);
    }

    [Fact]
    public async Task DeleteAsync_MissingSupplier_ReturnsFailure()
    {
        var (service, _) = CreateService();

        var result = await service.DeleteAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_NameTooLong_ReturnsFailure_WithoutTouchingRepository()
    {
        var (service, repo) = CreateService();
        var tooLong = new string('a', 257);

        var result = await service.CreateAsync(new SaveSupplierRequest(tooLong));

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.InvalidName", result.Error.Code);
        Assert.Empty(repo.Suppliers);
    }

    private static (SupplierService Service, FakeSupplierRepository Repository) CreateService()
    {
        var repository = new FakeSupplierRepository();
        var service = new SupplierService(repository, NullLogger<SupplierService>.Instance);
        return (service, repository);
    }
}
