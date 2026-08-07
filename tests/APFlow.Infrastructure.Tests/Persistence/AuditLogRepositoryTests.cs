using APFlow.Application.DTOs;
using APFlow.Application.Features.Approval;
using APFlow.Application.Features.Audit;
using APFlow.Application.Features.Invoices;
using APFlow.Application.Features.Workflow;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;
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
        var auditService = new AuditService(auditLogRepository, new FakeCurrentUserService(tenantId, "user-42"), NullLogger<AuditService>.Instance);
        var approvalAuthorizationService = new ApprovalAuthorizationService(new ApprovalPolicyRepository(context));
        var invoiceService = new InvoiceService(
            invoiceRepository, new SupplierRepository(context), auditService,
            new FakeCurrentUserService(tenantId, "user-42"), approvalAuthorizationService,
            new WorkflowValidationService(new WorkflowTemplateRepository(context)), new FakeEmailSendService(), NullLogger<InvoiceService>.Instance);

        var supplier = new Supplier { Name = "Acme Ltd", TenantId = tenantId };
        context.Suppliers.Add(supplier);
        await context.SaveChangesAsync();

        var created = await invoiceService.CreateAsync(new CreateInvoiceRequest(
            supplier.Id, "INV-1", null, null, "GBP", 100m, 20m, 120m, null));
        Assert.True(created.IsSuccess);

        // CreateAsync itself now also stages an InvoiceCreated entry (WP-052 Part
        // C) - assert on the specific action this test is about, rather than
        // assuming zero entries overall (and avoid calling Remove on AuditLog,
        // which contradicts its own documented immutability - see AuditLog.cs).
        Assert.DoesNotContain(await context.AuditLogs.ToListAsync(), a => a.Action == AuditActions.InvoiceStatusChanged);

        // WP-053: transition enforcement is now LIVE, so this test must move the
        // invoice along an edge that genuinely exists in the confirmed graph.
        // RECEIVED -> EXTRACTED (what this test originally used, and what WP-053's
        // own task 5 assumed would still pass) is NOT such an edge - the confirmed
        // graph routes ingestion RECEIVED -> PROCESSING -> EXTRACTED. Stepping via
        // PROCESSING here rather than inventing an unconfirmed direct edge - see
        // docs/WP-053-Transition-Enforcement-Decisions.md.
        var toProcessing = await invoiceService.UpdateAsync(
            created.Value.Id,
            new UpdateInvoiceRequest("INV-1", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Processing, Notes: "Picked up for processing."));
        Assert.True(toProcessing.IsSuccess);

        var updateResult = await invoiceService.UpdateAsync(
            created.Value.Id,
            new UpdateInvoiceRequest("INV-1", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Extracted, Notes: "Extraction complete."));

        Assert.True(updateResult.IsSuccess);
        Assert.Equal(InvoiceStatusCodes.Extracted, updateResult.Value.Status);

        // Each UpdateAsync call (one SaveChangesAsync each) committed the invoice's
        // new status, the audit entry describing it, AND (WP-084) the mandatory
        // note's own InvoiceNote row and NoteAdded audit entry - all in the same
        // real transaction, proven against a real DbContext, not a fake. Five
        // entries total: CreateAsync's InvoiceCreated (WP-052 Part C), plus one
        // InvoiceStatusChanged + one NoteAdded per transition (Received ->
        // Processing -> Extracted).
        var auditEntries = await context.AuditLogs.ToListAsync();
        Assert.Equal(5, auditEntries.Count);

        var noteAddedEntries = auditEntries.Where(a => a.Action == AuditActions.NoteAdded).OrderBy(a => a.CreatedAtUtc).ToList();
        Assert.Equal(2, noteAddedEntries.Count);
        Assert.Equal("Picked up for processing.", noteAddedEntries[0].NewValue);
        Assert.Equal("Extraction complete.", noteAddedEntries[1].NewValue);

        // WP-084: the InvoiceNote rows themselves are also really there, correctly
        // linked to the invoice - not just the audit trail describing them.
        var notes = await context.InvoiceNotes.Where(n => n.InvoiceId == created.Value.Id).OrderBy(n => n.CreatedAtUtc).ToListAsync();
        Assert.Equal(2, notes.Count);
        Assert.Equal("Picked up for processing.", notes[0].Content);
        Assert.Equal("Extraction complete.", notes[1].Content);

        var statusChanges = auditEntries
            .Where(a => a.Action == AuditActions.InvoiceStatusChanged)
            .OrderBy(a => a.CreatedAtUtc)
            .ToList();
        Assert.Equal(2, statusChanges.Count);

        Assert.Equal(InvoiceStatusCodes.Received, statusChanges[0].PreviousValue);
        Assert.Equal(InvoiceStatusCodes.Processing, statusChanges[0].NewValue);

        var entry = statusChanges[1];
        Assert.Equal(nameof(Invoice), entry.EntityName);
        Assert.Equal(created.Value.Id, entry.EntityId);
        Assert.Equal(InvoiceStatusCodes.Processing, entry.PreviousValue);
        Assert.Equal(InvoiceStatusCodes.Extracted, entry.NewValue);

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

    // WP-092: proves PerformedByDisplayName round-trips through a real AppDbContext -
    // FakeAuditLogRepository-based AuditServiceTests can't demonstrate that, same
    // reasoning as this class's own doc comment.
    [Fact]
    public async Task AddAsync_CurrentUserHasDisplayName_PersistsPerformedByDisplayName()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId, currentUserId: "user-42");
        var auditLogRepository = new AuditLogRepository(context);
        var auditService = new AuditService(
            auditLogRepository, new FakeCurrentUserService(tenantId, "user-42", "Priya Shah"), NullLogger<AuditService>.Instance);

        var result = await auditService.LogAndSaveAsync(new RecordAuditLogRequest(
            "InvoiceStatusChanged", "Invoice", Guid.NewGuid(), "Received", "Extracted"));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(await context.AuditLogs.ToListAsync());
        Assert.Equal("Priya Shah", entry.PerformedByDisplayName);
    }

    // The "historical row" case: no name claim captured at staging time - this
    // must persist as null, not a crash or an empty string, so the read side has
    // something well-defined to fall back from.
    [Fact]
    public async Task AddAsync_CurrentUserHasNoDisplayName_PersistsNullPerformedByDisplayName()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId, currentUserId: "user-42");
        var auditLogRepository = new AuditLogRepository(context);
        var auditService = new AuditService(
            auditLogRepository, new FakeCurrentUserService(tenantId, "user-42"), NullLogger<AuditService>.Instance);

        var result = await auditService.LogAndSaveAsync(new RecordAuditLogRequest(
            "InvoiceStatusChanged", "Invoice", Guid.NewGuid(), "Received", "Extracted"));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(await context.AuditLogs.ToListAsync());
        Assert.Null(entry.PerformedByDisplayName);
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

        var context = new AppDbContext(options, new FakeCurrentUserService(tenantId, currentUserId));

        // Required for HasData seed rows (workflow templates/statuses/transitions)
        // to materialize under the InMemory provider - unlike a real SqlServer
        // database via migrations, it does not seed automatically on first query.
        // Needed here as of WP-053, since InvoiceService.UpdateAsync now enforces
        // transitions against the seeded graph.
        context.Database.EnsureCreated();

        return context;
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(Guid tenantId, string? userId, string? displayName = null)
        {
            TenantId = tenantId.ToString();
            UserId = userId;
            DisplayName = displayName;
        }

        public bool IsAuthenticated => UserId is not null;
        public string? UserId { get; }
        public string? Email => null;
        public string? DisplayName { get; }
        public string? TenantId { get; }
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }

    /// <summary>
    /// WP-031: not independently exercised by this test (no assertion here
    /// transitions an invoice to QUERY_RAISED) - only present so InvoiceService's
    /// constructor resolves. Always succeeds.
    /// </summary>
    private sealed class FakeEmailSendService : IEmailSendService
    {
        public Task<Result> SendAsync(SendEmailRequest request, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success());
    }
}
