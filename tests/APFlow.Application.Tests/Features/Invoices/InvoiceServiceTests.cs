using APFlow.Application.DTOs;
using APFlow.Application.Features.Invoices;
using APFlow.Application.Tests.Features;
using APFlow.Domain.Entities;
using APFlow.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace APFlow.Application.Tests.Features.Invoices;

public class InvoiceServiceTests
{
    [Fact]
    public async Task CreateAsync_UnknownSupplier_ReturnsFailure()
    {
        var (service, _, _) = CreateService();

        var result = await service.CreateAsync(new CreateInvoiceRequest(
            Guid.NewGuid(), "INV-1", null, null, "GBP", 100m, 20m, 120m, null));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoice.SupplierNotFound", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_KnownSupplier_Succeeds_StartsAsReceived()
    {
        var (service, _, supplierRepo) = CreateService();
        var supplier = new Supplier { Name = "Test Supplier" };
        supplierRepo.Suppliers.Add(supplier);

        var result = await service.CreateAsync(new CreateInvoiceRequest(
            supplier.Id, "INV-100", new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), "GBP", 1000m, 200m, 1200m, "graph-msg-id"));

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.Received, result.Value.Status);
        Assert.Equal(1200m, result.Value.GrossTotal);
        Assert.Equal("Test Supplier", result.Value.SupplierName);
    }

    [Fact]
    public async Task UpdateAsync_ExistingInvoice_UpdatesFieldsIncludingStatus()
    {
        var (service, invoiceRepo, supplierRepo) = CreateService();
        var supplier = new Supplier { Name = "Test Supplier" };
        supplierRepo.Suppliers.Add(supplier);
        var created = await service.CreateAsync(new CreateInvoiceRequest(supplier.Id, "INV-1", null, null, "GBP", 100m, 20m, 120m, null));

        var result = await service.UpdateAsync(created.Value.Id, new UpdateInvoiceRequest(
            "INV-1-REV", null, null, "GBP", 100m, 20m, 120m, InvoiceStatus.Approved));

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatus.Approved, result.Value.Status);
        Assert.Equal("INV-1-REV", result.Value.SupplierInvoiceNumber);
    }

    [Fact]
    public async Task UpdateAsync_MissingInvoice_ReturnsFailure()
    {
        var (service, _, _) = CreateService();

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateInvoiceRequest(null, null, null, null, null, null, null, InvoiceStatus.Approved));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoice.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task DeleteAsync_ExistingInvoice_Succeeds_RemovesFromRepository()
    {
        var (service, invoiceRepo, supplierRepo) = CreateService();
        var supplier = new Supplier { Name = "Test Supplier" };
        supplierRepo.Suppliers.Add(supplier);
        var created = await service.CreateAsync(new CreateInvoiceRequest(supplier.Id, "INV-1", null, null, "GBP", 100m, 20m, 120m, null));

        var result = await service.DeleteAsync(created.Value.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(invoiceRepo.Invoices);
    }

    [Fact]
    public async Task DeleteAsync_MissingInvoice_ReturnsFailure()
    {
        var (service, _, _) = CreateService();

        var result = await service.DeleteAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("Invoice.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task AddNoteAsync_ValidContent_AddsNoteToInvoice()
    {
        var (service, invoiceRepo, supplierRepo) = CreateService();
        var supplier = new Supplier { Name = "Test Supplier" };
        supplierRepo.Suppliers.Add(supplier);
        var created = await service.CreateAsync(new CreateInvoiceRequest(supplier.Id, "INV-1", null, null, "GBP", 100m, 20m, 120m, null));

        var result = await service.AddNoteAsync(created.Value.Id, "Looks correct, approved.");

        Assert.True(result.IsSuccess);
        Assert.Single(invoiceRepo.Invoices[0].Notes);
    }

    [Fact]
    public async Task AddNoteAsync_EmptyContent_ReturnsFailure()
    {
        var (service, _, supplierRepo) = CreateService();
        var supplier = new Supplier { Name = "Test Supplier" };
        supplierRepo.Suppliers.Add(supplier);
        var created = await service.CreateAsync(new CreateInvoiceRequest(supplier.Id, "INV-1", null, null, "GBP", 100m, 20m, 120m, null));

        var result = await service.AddNoteAsync(created.Value.Id, "");

        Assert.True(result.IsFailure);
        Assert.Equal("Invoice.InvalidNoteContent", result.Error.Code);
    }

    [Fact]
    public async Task AddNoteAsync_MissingInvoice_ReturnsFailure()
    {
        var (service, _, _) = CreateService();

        var result = await service.AddNoteAsync(Guid.NewGuid(), "test");

        Assert.True(result.IsFailure);
        Assert.Equal("Invoice.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetByIdAsync_MissingInvoice_ReturnsFailure()
    {
        var (service, _, _) = CreateService();

        var result = await service.GetByIdAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("Invoice.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task CreateAsync_SupplierInvoiceNumberTooLong_ReturnsFailure_WithoutTouchingRepository()
    {
        var (service, invoiceRepo, supplierRepo) = CreateService();
        var supplier = new Supplier { Name = "Test Supplier" };
        supplierRepo.Suppliers.Add(supplier);
        var tooLong = new string('a', 129);

        var result = await service.CreateAsync(new CreateInvoiceRequest(supplier.Id, tooLong, null, null, "GBP", 100m, 20m, 120m, null));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoice.InvalidSupplierInvoiceNumber", result.Error.Code);
        Assert.Empty(invoiceRepo.Invoices);
    }

    [Fact]
    public async Task CreateAsync_CurrencyWrongLength_ReturnsFailure()
    {
        var (service, invoiceRepo, supplierRepo) = CreateService();
        var supplier = new Supplier { Name = "Test Supplier" };
        supplierRepo.Suppliers.Add(supplier);

        var result = await service.CreateAsync(new CreateInvoiceRequest(supplier.Id, "INV-1", null, null, "POUNDS", 100m, 20m, 120m, null));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoice.InvalidCurrency", result.Error.Code);
        Assert.Empty(invoiceRepo.Invoices);
    }

    [Fact]
    public async Task CreateAsync_NullCurrency_IsValid()
    {
        // Currency is optional - null/absent must not be rejected, only a
        // present-but-wrong-length value should be.
        var (service, _, supplierRepo) = CreateService();
        var supplier = new Supplier { Name = "Test Supplier" };
        supplierRepo.Suppliers.Add(supplier);

        var result = await service.CreateAsync(new CreateInvoiceRequest(supplier.Id, "INV-1", null, null, null, 100m, 20m, 120m, null));

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task UpdateAsync_SupplierInvoiceNumberTooLong_ReturnsFailure()
    {
        var (service, _, supplierRepo) = CreateService();
        var supplier = new Supplier { Name = "Test Supplier" };
        supplierRepo.Suppliers.Add(supplier);
        var created = await service.CreateAsync(new CreateInvoiceRequest(supplier.Id, "INV-1", null, null, "GBP", 100m, 20m, 120m, null));
        var tooLong = new string('a', 129);

        var result = await service.UpdateAsync(created.Value.Id, new UpdateInvoiceRequest(tooLong, null, null, "GBP", 100m, 20m, 120m, InvoiceStatus.Received));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoice.InvalidSupplierInvoiceNumber", result.Error.Code);
    }

    [Fact]
    public async Task AddNoteAsync_ContentTooLong_ReturnsFailure()
    {
        var (service, invoiceRepo, supplierRepo) = CreateService();
        var supplier = new Supplier { Name = "Test Supplier" };
        supplierRepo.Suppliers.Add(supplier);
        var created = await service.CreateAsync(new CreateInvoiceRequest(supplier.Id, "INV-1", null, null, "GBP", 100m, 20m, 120m, null));
        var tooLong = new string('a', 4001);

        var result = await service.AddNoteAsync(created.Value.Id, tooLong);

        Assert.True(result.IsFailure);
        Assert.Equal("Invoice.InvalidNoteContent", result.Error.Code);
        Assert.Empty(invoiceRepo.Invoices[0].Notes);
    }

    private static (InvoiceService Service, FakeInvoiceRepository InvoiceRepository, FakeSupplierRepository SupplierRepository) CreateService()
    {
        var invoiceRepository = new FakeInvoiceRepository();
        var supplierRepository = new FakeSupplierRepository();
        var service = new InvoiceService(invoiceRepository, supplierRepository, NullLogger<InvoiceService>.Instance);
        return (service, invoiceRepository, supplierRepository);
    }
}
