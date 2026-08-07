using APFlow.Application.DTOs;
using APFlow.Application.Features.Suppliers;
using APFlow.Application.Tests.Features;
using APFlow.Domain.Common.Constants;
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

    [Fact]
    public async Task CreateAsync_NoOptionalFieldsSupplied_DefaultsToActiveWithNoOtherFieldsSet()
    {
        var (service, _) = CreateService();

        var result = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd"));

        Assert.True(result.IsSuccess);
        Assert.Equal(SupplierStatusCodes.Active, result.Value.Status);
        Assert.Null(result.Value.Code);
        Assert.Null(result.Value.Email);
        Assert.Null(result.Value.Phone);
        Assert.Null(result.Value.CreditLimit);
        Assert.Null(result.Value.PaymentTermsDays);
        Assert.Null(result.Value.AccountingReference);
    }

    [Fact]
    public async Task CreateAsync_AllFieldsSupplied_PersistsThemAll()
    {
        var (service, repo) = CreateService();

        var result = await service.CreateAsync(new SaveSupplierRequest(
            "Acme Ltd", Code: "AC01", Email: "ap@acme.example", Phone: "0161 000 0000", CreditLimit: 5000m, PaymentTermsDays: 30, AccountingReference: "ACME01", Status: SupplierStatusCodes.Inactive));

        Assert.True(result.IsSuccess);
        Assert.Equal("AC01", result.Value.Code);
        Assert.Equal("ap@acme.example", result.Value.Email);
        Assert.Equal("0161 000 0000", result.Value.Phone);
        Assert.Equal(5000m, result.Value.CreditLimit);
        Assert.Equal(30, result.Value.PaymentTermsDays);
        Assert.Equal("ACME01", result.Value.AccountingReference);
        Assert.Equal(SupplierStatusCodes.Inactive, result.Value.Status);
        var persisted = Assert.Single(repo.Suppliers);
        Assert.Equal("AC01", persisted.Code);
        Assert.Equal("ap@acme.example", persisted.Email);
        Assert.Equal("0161 000 0000", persisted.Phone);
        Assert.Equal(5000m, persisted.CreditLimit);
        Assert.Equal(30, persisted.PaymentTermsDays);
        Assert.Equal("ACME01", persisted.AccountingReference);
        Assert.Equal(SupplierStatusCodes.Inactive, persisted.Status);
    }

    [Fact]
    public async Task CreateAsync_CodeTooLong_ReturnsFailure_WithoutTouchingRepository()
    {
        var (service, repo) = CreateService();
        var tooLong = new string('a', 33);

        var result = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd", Code: tooLong));

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.InvalidCode", result.Error.Code);
        Assert.Empty(repo.Suppliers);
    }

    [Fact]
    public async Task CreateAsync_EmailTooLong_ReturnsFailure_WithoutTouchingRepository()
    {
        var (service, repo) = CreateService();
        var tooLong = new string('a', 257);

        var result = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd", Email: tooLong));

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.InvalidEmail", result.Error.Code);
        Assert.Empty(repo.Suppliers);
    }

    [Fact]
    public async Task CreateAsync_PhoneTooLong_ReturnsFailure_WithoutTouchingRepository()
    {
        var (service, repo) = CreateService();
        var tooLong = new string('a', 33);

        var result = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd", Phone: tooLong));

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.InvalidPhone", result.Error.Code);
        Assert.Empty(repo.Suppliers);
    }

    [Fact]
    public async Task CreateAsync_NegativeCreditLimit_ReturnsFailure_WithoutTouchingRepository()
    {
        var (service, repo) = CreateService();

        var result = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd", CreditLimit: -1m));

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.InvalidCreditLimit", result.Error.Code);
        Assert.Empty(repo.Suppliers);
    }

    [Fact]
    public async Task CreateAsync_NegativePaymentTermsDays_ReturnsFailure_WithoutTouchingRepository()
    {
        var (service, repo) = CreateService();

        var result = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd", PaymentTermsDays: -1));

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.InvalidPaymentTerms", result.Error.Code);
        Assert.Empty(repo.Suppliers);
    }

    [Fact]
    public async Task CreateAsync_AccountingReferenceTooLong_ReturnsFailure_WithoutTouchingRepository()
    {
        var (service, repo) = CreateService();
        var tooLong = new string('a', 65);

        var result = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd", AccountingReference: tooLong));

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.InvalidAccountingReference", result.Error.Code);
        Assert.Empty(repo.Suppliers);
    }

    [Fact]
    public async Task CreateAsync_UnrecognizedStatus_ReturnsFailure_WithoutTouchingRepository()
    {
        var (service, repo) = CreateService();

        var result = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd", Status: "SUSPENDED"));

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.InvalidStatus", result.Error.Code);
        Assert.Empty(repo.Suppliers);
    }

    [Fact]
    public async Task UpdateAsync_ExistingSupplier_UpdatesAllFields()
    {
        var (service, _) = CreateService();
        var created = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd"));

        var result = await service.UpdateAsync(
            created.Value.Id,
            new SaveSupplierRequest("Acme Renamed", CreditLimit: 10000m, PaymentTermsDays: 60, AccountingReference: "ACME02", Status: SupplierStatusCodes.Inactive));

        Assert.True(result.IsSuccess);
        Assert.Equal("Acme Renamed", result.Value.Name);
        Assert.Equal(10000m, result.Value.CreditLimit);
        Assert.Equal(60, result.Value.PaymentTermsDays);
        Assert.Equal("ACME02", result.Value.AccountingReference);
        Assert.Equal(SupplierStatusCodes.Inactive, result.Value.Status);
    }

    [Fact]
    public async Task CreateAsync_NonFinanceManagerSetsCreditLimit_ReturnsForbidden_WithoutTouchingRepository()
    {
        var (service, repo) = CreateService(isFinanceManager: false);

        var result = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd", CreditLimit: 5000m));

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.CreditLimitForbidden", result.Error.Code);
        Assert.Empty(repo.Suppliers);
    }

    [Fact]
    public async Task CreateAsync_NonFinanceManagerOmitsCreditLimit_Succeeds()
    {
        var (service, repo) = CreateService(isFinanceManager: false);

        var result = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd", Code: "AC01", Email: "ap@acme.example"));

        Assert.True(result.IsSuccess);
        Assert.Single(repo.Suppliers);
    }

    [Fact]
    public async Task UpdateAsync_NonFinanceManagerChangesCreditLimit_ReturnsForbidden_LeavesSupplierUnchanged()
    {
        var (service, repo) = CreateService();
        var created = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd", CreditLimit: 5000m));
        var (nonFinanceService, _) = ServiceOver(repo, isFinanceManager: false);

        var result = await nonFinanceService.UpdateAsync(created.Value.Id, new SaveSupplierRequest("Acme Ltd", CreditLimit: 10000m));

        Assert.True(result.IsFailure);
        Assert.Equal("Supplier.CreditLimitForbidden", result.Error.Code);
        Assert.Equal(5000m, repo.Suppliers.Single().CreditLimit);
    }

    [Fact]
    public async Task UpdateAsync_NonFinanceManagerLeavesCreditLimitUnchanged_SucceedsForEveryOtherField()
    {
        var (service, repo) = CreateService();
        var created = await service.CreateAsync(new SaveSupplierRequest("Acme Ltd", CreditLimit: 5000m));
        var (nonFinanceService, _) = ServiceOver(repo, isFinanceManager: false);

        var result = await nonFinanceService.UpdateAsync(
            created.Value.Id, new SaveSupplierRequest("Acme Renamed", CreditLimit: 5000m, Email: "new-ap@acme.example"));

        Assert.True(result.IsSuccess);
        Assert.Equal("Acme Renamed", result.Value.Name);
        Assert.Equal("new-ap@acme.example", result.Value.Email);
        Assert.Equal(5000m, result.Value.CreditLimit);
    }

    private static (SupplierService Service, FakeSupplierRepository Repository) ServiceOver(FakeSupplierRepository repository, bool isFinanceManager)
    {
        var currentUser = new FakeCurrentUserService();
        if (isFinanceManager)
        {
            currentUser.RolesList.Add(Roles.FinanceManager);
        }

        return (new SupplierService(repository, currentUser, NullLogger<SupplierService>.Instance), repository);
    }

    /// <summary>
    /// Defaults the caller to FINANCE_MANAGER so every test not specifically about
    /// WP-026 task 5's CreditLimit permission split can set CreditLimit freely, same
    /// as before that check existed. Tests exercising the split itself pass
    /// <paramref name="isFinanceManager"/>: false.
    /// </summary>
    private static (SupplierService Service, FakeSupplierRepository Repository) CreateService(bool isFinanceManager = true)
    {
        var repository = new FakeSupplierRepository();
        var currentUser = new FakeCurrentUserService();
        if (isFinanceManager)
        {
            currentUser.RolesList.Add(Roles.FinanceManager);
        }

        var service = new SupplierService(repository, currentUser, NullLogger<SupplierService>.Instance);
        return (service, repository);
    }
}
