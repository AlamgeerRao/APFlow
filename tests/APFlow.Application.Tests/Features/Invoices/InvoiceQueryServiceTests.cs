using APFlow.Application.DTOs;
using APFlow.Application.Features.Invoices;
using APFlow.Application.Tests.Features;
using APFlow.Domain.Entities;
using APFlow.Domain.Enums;
using Xunit;

namespace APFlow.Application.Tests.Features.Invoices;

public class InvoiceQueryServiceTests
{
    [Fact]
    public void DefaultParameters_SortByCreatedAtUtcDescending()
    {
        // CreatedAtUtc itself is stamped only by AppDbContext.SaveChanges (its setter
        // is internal - see AuditEntity.cs - and is unreachable from this test
        // project by design), so the default-sort-field behavior is asserted at the
        // parameters level here; InvoiceRepositoryQueryTests (APFlow.Infrastructure.Tests)
        // exercises the real CreatedAtUtc-descending ordering end to end against a
        // context that actually stamps it.
        var parameters = new InvoiceQueryParameters();

        Assert.Equal(InvoiceSortField.CreatedAtUtc, parameters.SortBy);
        Assert.True(parameters.SortDescending);
    }

    [Fact]
    public async Task SearchAsync_NoFilters_ReturnsAllInvoices()
    {
        var (service, repo) = CreateService();
        var supplier = new Supplier { Name = "Acme" };
        repo.Invoices.AddRange([NewInvoice(supplier.Id), NewInvoice(supplier.Id)]);

        var result = await service.SearchAsync(new InvoiceQueryParameters());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
        Assert.Equal(2, result.Value.Items.Count);
    }

    [Fact]
    public async Task SearchAsync_FiltersByStatus()
    {
        var (service, repo) = CreateService();
        var supplier = new Supplier { Name = "Acme" };
        var received = NewInvoice(supplier.Id, status: InvoiceStatus.Received);
        var approved = NewInvoice(supplier.Id, status: InvoiceStatus.Approved);
        repo.Invoices.AddRange([received, approved]);

        var result = await service.SearchAsync(new InvoiceQueryParameters(Status: InvoiceStatus.Approved));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal(approved.Id, result.Value.Items[0].Id);
    }

    [Fact]
    public async Task SearchAsync_FiltersBySupplierId()
    {
        var (service, repo) = CreateService();
        var supplierA = Guid.NewGuid();
        var supplierB = Guid.NewGuid();
        var invoiceA = NewInvoice(supplierA);
        var invoiceB = NewInvoice(supplierB);
        repo.Invoices.AddRange([invoiceA, invoiceB]);

        var result = await service.SearchAsync(new InvoiceQueryParameters(SupplierId: supplierA));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal(invoiceA.Id, result.Value.Items[0].Id);
    }

    [Fact]
    public async Task SearchAsync_FiltersByDateRange_ExcludesInvoicesWithNoInvoiceDate()
    {
        var (service, repo) = CreateService();
        var supplier = Guid.NewGuid();
        var inRange = NewInvoice(supplier, invoiceDate: new DateOnly(2026, 3, 15));
        var beforeRange = NewInvoice(supplier, invoiceDate: new DateOnly(2026, 1, 1));
        var noDate = NewInvoice(supplier, invoiceDate: null);
        repo.Invoices.AddRange([inRange, beforeRange, noDate]);

        var result = await service.SearchAsync(new InvoiceQueryParameters(
            InvoiceDateFrom: new DateOnly(2026, 3, 1),
            InvoiceDateTo: new DateOnly(2026, 3, 31)));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal(inRange.Id, result.Value.Items[0].Id);
    }

    [Fact]
    public async Task SearchAsync_FiltersByInvoiceNumber_PartialMatch()
    {
        var (service, repo) = CreateService();
        var supplier = Guid.NewGuid();
        var match = NewInvoice(supplier, supplierInvoiceNumber: "INV-2026-00042");
        var noMatch = NewInvoice(supplier, supplierInvoiceNumber: "INV-2026-00099");
        repo.Invoices.AddRange([match, noMatch]);

        var result = await service.SearchAsync(new InvoiceQueryParameters(InvoiceNumber: "00042"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalCount);
        Assert.Equal(match.Id, result.Value.Items[0].Id);
    }

    [Fact]
    public async Task SearchAsync_AppliesPaging()
    {
        var (service, repo) = CreateService();
        var supplier = Guid.NewGuid();
        for (var i = 0; i < 5; i++)
        {
            repo.Invoices.Add(NewInvoice(supplier, invoiceDate: new DateOnly(2026, 1, 1 + i)));
        }

        var page1 = await service.SearchAsync(new InvoiceQueryParameters(Page: 1, PageSize: 2, SortBy: InvoiceSortField.InvoiceDate, SortDescending: false));
        var page2 = await service.SearchAsync(new InvoiceQueryParameters(Page: 2, PageSize: 2, SortBy: InvoiceSortField.InvoiceDate, SortDescending: false));

        Assert.True(page1.IsSuccess);
        Assert.True(page2.IsSuccess);
        Assert.Equal(5, page1.Value.TotalCount);
        Assert.Equal(3, page1.Value.TotalPages);
        Assert.Equal(2, page1.Value.Items.Count);
        Assert.Equal(2, page2.Value.Items.Count);
        Assert.NotEqual(page1.Value.Items[0].Id, page2.Value.Items[0].Id);
    }

    [Fact]
    public async Task SearchAsync_SortsAscendingWhenRequested()
    {
        var (service, repo) = CreateService();
        var supplier = Guid.NewGuid();
        var low = NewInvoice(supplier, grossTotal: 100m);
        var high = NewInvoice(supplier, grossTotal: 900m);
        repo.Invoices.AddRange([high, low]);

        var result = await service.SearchAsync(new InvoiceQueryParameters(SortBy: InvoiceSortField.GrossTotal, SortDescending: false));

        Assert.True(result.IsSuccess);
        Assert.Equal(low.Id, result.Value.Items[0].Id);
        Assert.Equal(high.Id, result.Value.Items[1].Id);
    }

    [Fact]
    public async Task SearchAsync_ReturnsLightweightDto_MappedFromEntity()
    {
        var (service, repo) = CreateService();
        var supplier = new Supplier { Name = "Test Supplier" };
        var invoice = NewInvoice(supplier.Id, supplierInvoiceNumber: "INV-1", grossTotal: 250m);
        invoice.Supplier = supplier;
        repo.Invoices.Add(invoice);

        var result = await service.SearchAsync(new InvoiceQueryParameters());

        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Value.Items);
        Assert.Equal(invoice.Id, dto.Id);
        Assert.Equal("Test Supplier", dto.SupplierName);
        Assert.Equal("INV-1", dto.SupplierInvoiceNumber);
        Assert.Equal(250m, dto.GrossTotal);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SearchAsync_InvalidPage_ReturnsFailure(int page)
    {
        var (service, _) = CreateService();

        var result = await service.SearchAsync(new InvoiceQueryParameters(Page: page));

        Assert.True(result.IsFailure);
        Assert.Equal("InvoiceQuery.InvalidPage", result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task SearchAsync_InvalidPageSize_ReturnsFailure(int pageSize)
    {
        var (service, _) = CreateService();

        var result = await service.SearchAsync(new InvoiceQueryParameters(PageSize: pageSize));

        Assert.True(result.IsFailure);
        Assert.Equal("InvoiceQuery.InvalidPageSize", result.Error.Code);
    }

    [Fact]
    public async Task SearchAsync_InvoiceDateFromAfterInvoiceDateTo_ReturnsFailure()
    {
        var (service, _) = CreateService();

        var result = await service.SearchAsync(new InvoiceQueryParameters(
            InvoiceDateFrom: new DateOnly(2026, 6, 1),
            InvoiceDateTo: new DateOnly(2026, 1, 1)));

        Assert.True(result.IsFailure);
        Assert.Equal("InvoiceQuery.InvalidDateRange", result.Error.Code);
    }

    private static Invoice NewInvoice(
        Guid supplierId,
        InvoiceStatus status = InvoiceStatus.Received,
        DateOnly? invoiceDate = null,
        string? supplierInvoiceNumber = null,
        decimal? grossTotal = null) =>
        new()
        {
            SupplierId = supplierId,
            Status = status,
            InvoiceDate = invoiceDate,
            SupplierInvoiceNumber = supplierInvoiceNumber,
            GrossTotal = grossTotal,
        };

    private static (InvoiceQueryService Service, FakeInvoiceRepository Repository) CreateService()
    {
        var repository = new FakeInvoiceRepository();
        return (new InvoiceQueryService(repository), repository);
    }
}
