using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;
using APFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace APFlow.Infrastructure.Tests.Persistence;

/// <summary>
/// Exercises InvoiceRepository.PersistDuplicateCheckResultAsync (WP-048) against a
/// real AppDbContext (InMemory provider - same approach as
/// InvoiceRepositoryQueryTests), proving the fetch-mutate-save-immediately behavior
/// actually persists, not just that the code compiles.
/// </summary>
public class InvoiceRepositoryPersistDuplicateCheckResultTests
{
    [Fact]
    public async Task PersistDuplicateCheckResultAsync_ExistingInvoice_PersistsImmediately_ReturnsTrue()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new InvoiceRepository(context);
        var supplier = new Supplier { Name = "Acme Ltd", TenantId = tenantId };
        context.Suppliers.Add(supplier);
        var invoice = new Invoice { SupplierId = supplier.Id, TenantId = tenantId };
        context.Invoices.Add(invoice);
        await context.SaveChangesAsync();

        var persisted = await repository.PersistDuplicateCheckResultAsync(
            invoice.Id, isPotentialDuplicate: true, duplicateCheckReason: "Matches existing invoice on Supplier and Invoice Number.");

        Assert.True(persisted);

        // Read back via a fresh query (not the same tracked instance) to prove this
        // was actually committed, not just mutated in memory.
        var reloaded = await context.Invoices.AsNoTracking().SingleAsync(i => i.Id == invoice.Id);
        Assert.True(reloaded.IsPotentialDuplicate);
        Assert.Equal("Matches existing invoice on Supplier and Invoice Number.", reloaded.DuplicateCheckReason);
    }

    [Fact]
    public async Task PersistDuplicateCheckResultAsync_UnknownInvoiceId_ReturnsFalse_PersistsNothing()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new InvoiceRepository(context);

        var persisted = await repository.PersistDuplicateCheckResultAsync(
            Guid.NewGuid(), isPotentialDuplicate: true, duplicateCheckReason: "should not be written");

        Assert.False(persisted);
        Assert.Empty(await context.Invoices.ToListAsync());
    }

    [Fact]
    public async Task PersistDuplicateCheckResultAsync_RespectsTenantIsolation_CannotPersistAgainstAnotherTenantsInvoice()
    {
        var databaseName = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        Guid invoiceId;

        using (var contextA = CreateContext(tenantA, databaseName))
        {
            var supplierA = new Supplier { Name = "Tenant A Supplier", TenantId = tenantA };
            contextA.Suppliers.Add(supplierA);
            var invoiceA = new Invoice { SupplierId = supplierA.Id, TenantId = tenantA };
            contextA.Invoices.Add(invoiceA);
            await contextA.SaveChangesAsync();
            invoiceId = invoiceA.Id;
        }

        using var contextB = CreateContext(tenantB, databaseName);
        var repositoryB = new InvoiceRepository(contextB);

        var persisted = await repositoryB.PersistDuplicateCheckResultAsync(
            invoiceId, isPotentialDuplicate: true, duplicateCheckReason: "should not be visible to tenant B");

        Assert.False(persisted);
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
        public string? TenantId { get; }
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }
}
