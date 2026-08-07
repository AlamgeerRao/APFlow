using APFlow.Api.Controllers;
using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Common.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace APFlow.Api.Tests.Controllers;

/// <summary>
/// Exercises SuppliersController (WP-026) - the thin HTTP wrapper this WP added
/// over the existing, already-tested ISupplierService, so suppliers finally have a
/// real HTTP surface (previously only called internally - duplicate detection,
/// supplier-folder queries).
/// </summary>
public class SuppliersControllerTests
{
    private static readonly Guid SupplierId = Guid.NewGuid();

    [Fact]
    public async Task GetAll_ReturnsEverySupplierFromTheService()
    {
        var supplier = new SupplierDto(SupplierId, "Acme Ltd", "AC01", "ap@acme.example", "0161 000 0000", 5000m, 30, "ACME01", SupplierStatusCodes.Active, DateTimeOffset.UtcNow);
        var service = new FakeSupplierService { AllToReturn = [supplier] };
        var controller = new SuppliersController(service);

        var actionResult = await controller.GetAll(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsAssignableFrom<IReadOnlyList<SupplierDto>>(okResult.Value);
        var item = Assert.Single(response);
        Assert.Equal(SupplierId, item.Id);
        Assert.Equal("Acme Ltd", item.Name);
    }

    [Fact]
    public async Task GetById_ExistingSupplier_ReturnsIt()
    {
        var supplier = new SupplierDto(SupplierId, "Acme Ltd", null, null, null, null, null, null, SupplierStatusCodes.Active, DateTimeOffset.UtcNow);
        var service = new FakeSupplierService { ByIdToReturn = supplier };
        var controller = new SuppliersController(service);

        var actionResult = await controller.GetById(SupplierId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<SupplierDto>(okResult.Value);
        Assert.Equal(SupplierId, response.Id);
    }

    [Fact]
    public async Task GetById_MissingSupplier_ReturnsNotFoundWithCode()
    {
        var service = new FakeSupplierService
        {
            FailureToReturn = new Error("Supplier.NotFound", $"Supplier '{SupplierId}' was not found."),
        };
        var controller = new SuppliersController(service);

        var actionResult = await controller.GetById(SupplierId, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Supplier.NotFound", problemDetails.Extensions["code"]);
    }

    [Fact]
    public async Task Create_ValidRequest_ReturnsCreatedWithLocation()
    {
        var created = new SupplierDto(SupplierId, "Acme Ltd", "AC01", "ap@acme.example", "0161 000 0000", 5000m, 30, "ACME01", SupplierStatusCodes.Active, DateTimeOffset.UtcNow);
        var service = new FakeSupplierService { CreateResultToReturn = created };
        var controller = new SuppliersController(service);

        var actionResult = await controller.Create(
            new SaveSupplierRequest("Acme Ltd", "AC01", "ap@acme.example", "0161 000 0000", 5000m, 30, "ACME01", SupplierStatusCodes.Active), CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult);
        Assert.Equal(nameof(SuppliersController.GetById), createdResult.ActionName);
        Assert.Equal(SupplierId, ((SupplierDto)createdResult.Value!).Id);
    }

    [Fact]
    public async Task Create_InvalidRequest_ReturnsBadRequestWithCode()
    {
        var service = new FakeSupplierService
        {
            FailureToReturn = new Error("Supplier.InvalidName", "Supplier name must not be empty."),
        };
        var controller = new SuppliersController(service);

        var actionResult = await controller.Create(new SaveSupplierRequest(""), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Supplier.InvalidName", problemDetails.Extensions["code"]);
    }

    [Fact]
    public async Task Create_CreditLimitForbidden_ReturnsForbiddenWithCode()
    {
        var service = new FakeSupplierService
        {
            FailureToReturn = new Error("Supplier.CreditLimitForbidden", "Changing a supplier's credit limit requires the 'FINANCE_MANAGER' role."),
        };
        var controller = new SuppliersController(service);

        var actionResult = await controller.Create(new SaveSupplierRequest("Acme Ltd", CreditLimit: 5000m), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Supplier.CreditLimitForbidden", problemDetails.Extensions["code"]);
    }

    [Fact]
    public async Task Update_ExistingSupplier_ReturnsOkWithUpdatedSupplier()
    {
        var updated = new SupplierDto(SupplierId, "Acme Renamed", "AC02", "new-ap@acme.example", "0161 111 1111", 10000m, 60, "ACME02", SupplierStatusCodes.Inactive, DateTimeOffset.UtcNow);
        var service = new FakeSupplierService { UpdateResultToReturn = updated };
        var controller = new SuppliersController(service);

        var actionResult = await controller.Update(
            SupplierId, new SaveSupplierRequest("Acme Renamed", "AC02", "new-ap@acme.example", "0161 111 1111", 10000m, 60, "ACME02", SupplierStatusCodes.Inactive), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<SupplierDto>(okResult.Value);
        Assert.Equal("Acme Renamed", response.Name);
        Assert.Equal(SupplierStatusCodes.Inactive, response.Status);
    }

    [Fact]
    public async Task Update_MissingSupplier_ReturnsNotFoundWithCode()
    {
        var service = new FakeSupplierService
        {
            FailureToReturn = new Error("Supplier.NotFound", $"Supplier '{SupplierId}' was not found."),
        };
        var controller = new SuppliersController(service);

        var actionResult = await controller.Update(SupplierId, new SaveSupplierRequest("Acme Ltd"), CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
    }

    [Fact]
    public async Task Delete_ExistingSupplier_ReturnsNoContent()
    {
        var service = new FakeSupplierService { DeleteSucceeds = true };
        var controller = new SuppliersController(service);

        var actionResult = await controller.Delete(SupplierId, CancellationToken.None);

        Assert.IsType<NoContentResult>(actionResult);
    }

    [Fact]
    public async Task Delete_MissingSupplier_ReturnsNotFoundWithCode()
    {
        var service = new FakeSupplierService
        {
            FailureToReturn = new Error("Supplier.NotFound", $"Supplier '{SupplierId}' was not found."),
        };
        var controller = new SuppliersController(service);

        var actionResult = await controller.Delete(SupplierId, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Supplier.NotFound", problemDetails.Extensions["code"]);
    }

    private sealed class FakeSupplierService : ISupplierService
    {
        public IReadOnlyList<SupplierDto> AllToReturn { get; set; } = [];
        public SupplierDto? ByIdToReturn { get; set; }
        public SupplierDto? CreateResultToReturn { get; set; }
        public SupplierDto? UpdateResultToReturn { get; set; }
        public bool DeleteSucceeds { get; set; }
        public Error? FailureToReturn { get; set; }

        public Task<Result<SupplierDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(FailureToReturn is { } error
                ? Result.Failure<SupplierDto>(error)
                : Result.Success(ByIdToReturn!));

        public Task<Result<IReadOnlyList<SupplierDto>>> GetAllAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(FailureToReturn is { } error
                ? Result.Failure<IReadOnlyList<SupplierDto>>(error)
                : Result.Success(AllToReturn));

        public Task<Result<SupplierDto>> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(FailureToReturn is { } error
                ? Result.Failure<SupplierDto>(error)
                : Result.Success(CreateResultToReturn!));

        public Task<Result<SupplierDto>> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(FailureToReturn is { } error
                ? Result.Failure<SupplierDto>(error)
                : Result.Success(UpdateResultToReturn!));

        public Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(FailureToReturn is { } error
                ? Result.Failure(error)
                : Result.Success());
    }
}
