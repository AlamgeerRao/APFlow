using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;
using APFlow.Domain.Common.Constants;
using APFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace APFlow.Infrastructure.Tests.Persistence;

/// <summary>
/// Exercises InvoiceRepository.QueryAsync against a real AppDbContext (InMemory
/// provider - same approach as AppDbContextTenantIsolationTests) rather than the
/// hand-written fake used by InvoiceQueryServiceTests (APFlow.Application.Tests) -
/// this is where filtering/sorting/paging is proven against actual EF Core query
/// translation, including the tenant query filter staying in effect underneath it.
/// </summary>
public class InvoiceRepositoryQueryTests
{
    [Fact]
    public async Task QueryAsync_NoFilters_OrdersByCreatedAtUtcDescending_ByDefault()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new InvoiceRepository(context);
        var supplier = new Supplier { Name = "Acme", TenantId = tenantId };
        context.Suppliers.Add(supplier);

        var first = new Invoice { SupplierId = supplier.Id, TenantId = tenantId };
        context.Invoices.Add(first);
        await context.SaveChangesAsync(); // Stamps first.CreatedAtUtc.

        var second = new Invoice { SupplierId = supplier.Id, TenantId = tenantId };
        context.Invoices.Add(second);
        await context.SaveChangesAsync(); // Stamps second.CreatedAtUtc, strictly later.

        var (items, totalCount) = await repository.QueryAsync(new InvoiceQueryParameters());

        Assert.Equal(2, totalCount);
        Assert.Equal(second.Id, items[0].Id);
        Assert.Equal(first.Id, items[1].Id);
    }

    [Fact]
    public async Task QueryAsync_FiltersByStatus()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new InvoiceRepository(context);
        var supplier = new Supplier { Name = "Acme", TenantId = tenantId };
        context.Suppliers.Add(supplier);
        context.Invoices.AddRange(
            new Invoice { SupplierId = supplier.Id, TenantId = tenantId, Status = InvoiceStatusCodes.Received },
            new Invoice { SupplierId = supplier.Id, TenantId = tenantId, Status = InvoiceStatusCodes.Approved });
        await context.SaveChangesAsync();

        var (items, totalCount) = await repository.QueryAsync(new InvoiceQueryParameters(Status: InvoiceStatusCodes.Approved));

        Assert.Equal(1, totalCount);
        Assert.Equal(InvoiceStatusCodes.Approved, items[0].Status);
    }

    [Fact]
    public async Task QueryAsync_FiltersByMultipleStatuses()
    {
        // WP-074: the Query Queue nav view (NEEDS_QUERY/QUERY_RAISED/
        // AWAITING_SUPPLIER_RESPONSE combined) reuses this same endpoint via the
        // new Statuses parameter, rather than needing a new one.
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new InvoiceRepository(context);
        var supplier = new Supplier { Name = "Acme", TenantId = tenantId };
        context.Suppliers.Add(supplier);
        context.Invoices.AddRange(
            new Invoice { SupplierId = supplier.Id, TenantId = tenantId, Status = InvoiceStatusCodes.NeedsQuery },
            new Invoice { SupplierId = supplier.Id, TenantId = tenantId, Status = InvoiceStatusCodes.QueryRaised },
            new Invoice { SupplierId = supplier.Id, TenantId = tenantId, Status = InvoiceStatusCodes.AwaitingSupplierResponse },
            new Invoice { SupplierId = supplier.Id, TenantId = tenantId, Status = InvoiceStatusCodes.Received },
            new Invoice { SupplierId = supplier.Id, TenantId = tenantId, Status = InvoiceStatusCodes.Approved });
        await context.SaveChangesAsync();

        var (items, totalCount) = await repository.QueryAsync(new InvoiceQueryParameters(
            Statuses: [InvoiceStatusCodes.NeedsQuery, InvoiceStatusCodes.QueryRaised, InvoiceStatusCodes.AwaitingSupplierResponse]));

        Assert.Equal(3, totalCount);
        Assert.All(items, i => Assert.Contains(
            i.Status, new[] { InvoiceStatusCodes.NeedsQuery, InvoiceStatusCodes.QueryRaised, InvoiceStatusCodes.AwaitingSupplierResponse }));
    }

    [Fact]
    public async Task QueryAsync_StatusesTakesPrecedenceOverStatus_WhenBothGiven()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new InvoiceRepository(context);
        var supplier = new Supplier { Name = "Acme", TenantId = tenantId };
        context.Suppliers.Add(supplier);
        context.Invoices.AddRange(
            new Invoice { SupplierId = supplier.Id, TenantId = tenantId, Status = InvoiceStatusCodes.NeedsQuery },
            new Invoice { SupplierId = supplier.Id, TenantId = tenantId, Status = InvoiceStatusCodes.Received });
        await context.SaveChangesAsync();

        var (items, totalCount) = await repository.QueryAsync(new InvoiceQueryParameters(
            Status: InvoiceStatusCodes.Received,
            Statuses: [InvoiceStatusCodes.NeedsQuery]));

        Assert.Equal(1, totalCount);
        Assert.Equal(InvoiceStatusCodes.NeedsQuery, items[0].Status);
    }

    [Fact]
    public async Task QueryAsync_FiltersByDateRange()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new InvoiceRepository(context);
        var supplier = new Supplier { Name = "Acme", TenantId = tenantId };
        context.Suppliers.Add(supplier);
        var inRange = new Invoice { SupplierId = supplier.Id, TenantId = tenantId, InvoiceDate = new DateOnly(2026, 3, 15) };
        var outOfRange = new Invoice { SupplierId = supplier.Id, TenantId = tenantId, InvoiceDate = new DateOnly(2026, 5, 1) };
        context.Invoices.AddRange(inRange, outOfRange);
        await context.SaveChangesAsync();

        var (items, totalCount) = await repository.QueryAsync(new InvoiceQueryParameters(
            InvoiceDateFrom: new DateOnly(2026, 3, 1),
            InvoiceDateTo: new DateOnly(2026, 3, 31)));

        Assert.Equal(1, totalCount);
        Assert.Equal(inRange.Id, items[0].Id);
    }

    [Fact]
    public async Task QueryAsync_FiltersByInvoiceNumber_SubstringMatch()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new InvoiceRepository(context);
        var supplier = new Supplier { Name = "Acme", TenantId = tenantId };
        context.Suppliers.Add(supplier);
        var match = new Invoice { SupplierId = supplier.Id, TenantId = tenantId, SupplierInvoiceNumber = "INV-2026-00042" };
        var noMatch = new Invoice { SupplierId = supplier.Id, TenantId = tenantId, SupplierInvoiceNumber = "INV-2026-00099" };
        context.Invoices.AddRange(match, noMatch);
        await context.SaveChangesAsync();

        var (items, totalCount) = await repository.QueryAsync(new InvoiceQueryParameters(InvoiceNumber: "00042"));

        Assert.Equal(1, totalCount);
        Assert.Equal(match.Id, items[0].Id);
    }

    [Fact]
    public async Task QueryAsync_FiltersBySearch_MatchesSupplierNameOrInvoiceNumber()
    {
        // WP-015 ruling, 2026-07-25 item 3: search matches supplier name OR
        // invoice number - a real EF Core translation check, not just the fake.
        // Casing kept consistent with the search term throughout: the InMemory
        // provider evaluates Contains via plain (case-sensitive) LINQ-to-Objects,
        // unlike a real SQL Server's typically case-insensitive collation (see
        // InvoiceQueryParameters.InvoiceNumber's own doc comment on this
        // provider-vs-real-database distinction).
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new InvoiceRepository(context);
        var acme = new Supplier { Name = "Acme Ltd", TenantId = tenantId };
        var northwind = new Supplier { Name = "Northwind", TenantId = tenantId };
        context.Suppliers.AddRange(acme, northwind);
        var matchBySupplier = new Invoice { SupplierId = acme.Id, TenantId = tenantId, SupplierInvoiceNumber = "INV-001" };
        var matchByNumber = new Invoice { SupplierId = northwind.Id, TenantId = tenantId, SupplierInvoiceNumber = "Acme-777" };
        var noMatch = new Invoice { SupplierId = northwind.Id, TenantId = tenantId, SupplierInvoiceNumber = "INV-999" };
        context.Invoices.AddRange(matchBySupplier, matchByNumber, noMatch);
        await context.SaveChangesAsync();

        var (items, totalCount) = await repository.QueryAsync(new InvoiceQueryParameters(Search: "Acme"));

        Assert.Equal(2, totalCount);
        Assert.Contains(items, i => i.Id == matchBySupplier.Id);
        Assert.Contains(items, i => i.Id == matchByNumber.Id);
    }

    [Fact]
    public async Task QueryAsync_AppliesPaging_TotalCountReflectsFullFilteredSet()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new InvoiceRepository(context);
        var supplier = new Supplier { Name = "Acme", TenantId = tenantId };
        context.Suppliers.Add(supplier);
        for (var i = 0; i < 5; i++)
        {
            context.Invoices.Add(new Invoice { SupplierId = supplier.Id, TenantId = tenantId, InvoiceDate = new DateOnly(2026, 1, 1 + i) });
        }

        await context.SaveChangesAsync();

        var (page1Items, totalCount) = await repository.QueryAsync(new InvoiceQueryParameters(
            Page: 1, PageSize: 2, SortBy: InvoiceSortField.InvoiceDate, SortDescending: false));
        var (page2Items, _) = await repository.QueryAsync(new InvoiceQueryParameters(
            Page: 2, PageSize: 2, SortBy: InvoiceSortField.InvoiceDate, SortDescending: false));

        Assert.Equal(5, totalCount);
        Assert.Equal(2, page1Items.Count);
        Assert.Equal(2, page2Items.Count);
        Assert.Equal(new DateOnly(2026, 1, 1), page1Items[0].InvoiceDate);
        Assert.Equal(new DateOnly(2026, 1, 3), page2Items[0].InvoiceDate);
    }

    [Fact]
    public async Task QueryAsync_SortsBySupplierName_UsingJoinedNavigationProperty()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new InvoiceRepository(context);
        var supplierA = new Supplier { Name = "Aardvark Ltd", TenantId = tenantId };
        var supplierZ = new Supplier { Name = "Zebra Ltd", TenantId = tenantId };
        context.Suppliers.AddRange(supplierA, supplierZ);
        var invoiceA = new Invoice { SupplierId = supplierA.Id, TenantId = tenantId };
        var invoiceZ = new Invoice { SupplierId = supplierZ.Id, TenantId = tenantId };
        context.Invoices.AddRange(invoiceZ, invoiceA);
        await context.SaveChangesAsync();

        var (items, _) = await repository.QueryAsync(new InvoiceQueryParameters(SortBy: InvoiceSortField.SupplierName, SortDescending: false));

        Assert.Equal(invoiceA.Id, items[0].Id);
        Assert.Equal(invoiceZ.Id, items[1].Id);
    }

    [Fact]
    public async Task QueryAsync_RespectsTenantIsolation()
    {
        var databaseName = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var contextA = CreateContext(tenantA, databaseName))
        {
            var supplierA = new Supplier { Name = "Tenant A Supplier", TenantId = tenantA };
            contextA.Suppliers.Add(supplierA);
            contextA.Invoices.Add(new Invoice { SupplierId = supplierA.Id, TenantId = tenantA });
            await contextA.SaveChangesAsync();
        }

        using (var contextB = CreateContext(tenantB, databaseName))
        {
            var supplierB = new Supplier { Name = "Tenant B Supplier", TenantId = tenantB };
            contextB.Suppliers.Add(supplierB);
            contextB.Invoices.Add(new Invoice { SupplierId = supplierB.Id, TenantId = tenantB });
            await contextB.SaveChangesAsync();
        }

        using var queryAsA = CreateContext(tenantA, databaseName);
        var repository = new InvoiceRepository(queryAsA);

        var (items, totalCount) = await repository.QueryAsync(new InvoiceQueryParameters());

        Assert.Equal(1, totalCount);
        Assert.Single(items);
    }

    [Fact]
    public async Task QueryAsync_PageSizeAboveMax_IsClampedDefensively()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new InvoiceRepository(context);
        var supplier = new Supplier { Name = "Acme", TenantId = tenantId };
        context.Suppliers.Add(supplier);
        for (var i = 0; i < 3; i++)
        {
            context.Invoices.Add(new Invoice { SupplierId = supplier.Id, TenantId = tenantId });
        }

        await context.SaveChangesAsync();

        // Bypasses IInvoiceQueryService's validation (which would reject this) to
        // prove the repository's own defensive clamp - see
        // InvoiceQueryParameters.MaxPageSize's doc comment.
        var (items, totalCount) = await repository.QueryAsync(new InvoiceQueryParameters(PageSize: 1000));

        Assert.Equal(3, totalCount);
        Assert.Equal(3, items.Count); // Fewer rows exist than even the clamped max - clamp doesn't force extra rows.
    }

    private static AppDbContext CreateContext(Guid tenantId, string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options, new FakeCurrentUserService(tenantId));
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(Guid tenantId)
        {
            TenantId = tenantId.ToString();
        }

        public bool IsAuthenticated => true;
        public string? UserId => "test-user";
        public string? Email => null;
        public string? DisplayName => null;
        public string? TenantId { get; }
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }
}
