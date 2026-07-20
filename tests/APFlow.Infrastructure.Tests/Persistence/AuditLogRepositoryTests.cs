using APFlow.Application.DTOs;
using APFlow.Application.Features.Audit;
using APFlow.Application.Features.Invoices;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;
using APFlow.Domain.Enums;
using APFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace APFlow.Infrastructure.Tests.Persistence;

/// <summary>
/// Exercises AuditLogRepository against a real AppDbContext (InMemory provider -
/// same approach as InvoiceRepositoryQueryTests), including the WP-013 design's
/// central claim: that IAuditService.LogAsync's staged entry commits atomically with
/// whatever change it describes, via the caller's own single SaveChangesAsync call -
/// not via any save call of its own. FakeAuditLogRepository-based tests
/// (APFlow.Application.Tests) cannot prove this - two fakes sharing no real
/// DbContext can't demonstrate a real shared-transaction commit.
/// </summary>
public class AuditLogRepositoryTests
{
    [Fact]
    public async Task UpdateInvoiceStatus_RealContext_CommitsInvoiceAndAuditEntryTogether_InOneSaveChangesCall()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId, currentUserId: "user-42");
        var invoiceRepository = new InvoiceRepository(context);
        var auditLogRepository = new AuditLogRepository(context);
        var auditService = new AuditService(auditLogRepository, NullLogger<AuditService>.Instance);
        var invoiceService = new InvoiceService(invoiceRepository, new SupplierRepository(context), auditService, NullLogger<InvoiceService>.Instance);

        var supplier = new Supplier { Name = "Acme Ltd", TenantId = tenantId };
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();

        var created = await invoiceService.CreateAsync(new CreateInvoiceRequest(
            supplier.Id, "INV-1", null, null, "GBP", 100m, 20m, 120m, null));
        Assert.True(created.IsSuccess);

        // Before the status-changing UpdateAsync call: no audit entries exist yet.
        Assert.Empty(await context.AuditLogs.ToListAsync());

        var updateResult = await invoiceService.UpdateAsync(
            created.Value.Id,
            new UpdateInvoiceRequest("INV-1", null, null, "GBP", 100m, 20m, 120m, InvoiceStatus.Extracted));

        Assert.True(updateResult.IsSuccess);
        Assert.Equal(InvoiceStatus.Extracted, updateResult.Value.Status);

        // The single UpdateAsync call (one SaveChangesAsync) committed BOTH the
        // invoice's new status AND the audit entry describing it - this is the
        // atomic-commit design's whole point, proven against a real DbContext.
        var auditEntries = await context.AuditLogs.ToListAsync();
        var entry = Assert.Single(auditEntries);
        Assert.Equal(AuditActions.InvoiceStatusChanged, entry.Action);
        Assert.Equal(nameof(Invoice), entry.EntityName);
        Assert.Equal(created.Value.Id, entry.EntityId);
        Assert.Equal(InvoiceStatus.Received.ToString(), entry.PreviousValue);
        Assert.Equal(InvoiceStatus.Extracted.ToString(), entry.NewValue);

        // Real AppDbContext.SaveChanges stamping - "User" and "Date/Time" come from
        // AuditEntity.CreatedBy/CreatedAtUtc, not a dedicated column (see AuditLog's
        // doc comment).
        Assert.Equal("user-42", entry.CreatedBy);
        Assert.NotEqual(default, entry.CreatedAtUtc);
    }

    [Fact]
    public async Task AddAsync_NoAuthenticatedUser_FallsBackToSystemSentinel()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId, currentUserId: null);
        var repository = new AuditLogRepository(context);

        await repository.AddAsync(new AuditLog
        {
            TenantId = tenantId,
            Action = "InvoiceStatusChanged",
            EntityName = "Invoice",
            EntityId = Guid.NewGuid(),
            PreviousValue = "Received",
            NewValue = "Extracted",
        });
        await repository.SaveChangesAsync();

        var entry = Assert.Single(await context.AuditLogs.ToListAsync());
        Assert.Equal("system", entry.CreatedBy);
    }

    [Fact]
    public async Task QueryAsync_FiltersByEntityNameAndEntityId()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new AuditLogRepository(context);
        var targetId = Guid.NewGuid();

        context.AuditLogs.AddRange(
            NewEntry(tenantId, "Invoice", targetId),
            NewEntry(tenantId, "Invoice", Guid.NewGuid()),
            NewEntry(tenantId, "Supplier", targetId));
        await context.SaveChangesAsync();

        var (items, totalCount) = await repository.QueryAsync(new AuditLogQueryParameters(EntityName: "Invoice", EntityId: targetId));

        Assert.Equal(1, totalCount);
        Assert.Equal(targetId, items[0].EntityId);
    }

    [Fact]
    public async Task QueryAsync_RespectsTenantIsolation()
    {
        var databaseName = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var contextA = CreateContext(tenantA, databaseName: databaseName))
        {
            contextA.AuditLogs.Add(NewEntry(tenantA, "Invoice", Guid.NewGuid()));
            await contextA.SaveChangesAsync();
        }

        using (var contextB = CreateContext(tenantB, databaseName: databaseName))
        {
            contextB.AuditLogs.Add(NewEntry(tenantB, "Invoice", Guid.NewGuid()));
            await contextB.SaveChangesAsync();
        }

        using var queryAsA = CreateContext(tenantA, databaseName: databaseName);
        var repository = new AuditLogRepository(queryAsA);

        var (items, totalCount) = await repository.QueryAsync(new AuditLogQueryParameters());

        Assert.Equal(1, totalCount);
        Assert.Single(items);
    }

    private static AuditLog NewEntry(Guid tenantId, string entityName, Guid entityId) => new()
    {
        TenantId = tenantId,
        Action = "InvoiceStatusChanged",
        EntityName = entityName,
        EntityId = entityId,
        PreviousValue = "Received",
        NewValue = "Extracted",
    };

    private static AppDbContext CreateContext(Guid tenantId, string? currentUserId = "test-user", string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options, new FakeCurrentUserService(tenantId, currentUserId));
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(Guid tenantId, string? userId)
        {
            TenantId = tenantId.ToString();
            UserId = userId;
        }

        public bool IsAuthenticated => UserId is not null;
        public string? UserId { get; }
        public string? Email => null;
        public string? TenantId { get; }
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }
}
