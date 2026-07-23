using APFlow.Application.DTOs;
using APFlow.Application.Features.Approval;
using APFlow.Application.Features.Audit;
using APFlow.Application.Features.Invoices;
using APFlow.Application.Tests.Features;
using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;
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
        Assert.Equal(InvoiceStatusCodes.Received, result.Value.Status);
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
            "INV-1-REV", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Approved));

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatusCodes.Approved, result.Value.Status);
        Assert.Equal("INV-1-REV", result.Value.SupplierInvoiceNumber);
    }

    [Fact]
    public async Task UpdateAsync_MissingInvoice_ReturnsFailure()
    {
        var (service, _, _) = CreateService();

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateInvoiceRequest(null, null, null, null, null, null, null, InvoiceStatusCodes.Approved));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoice.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_StatusChanged_StagesAuditLogEntry_CommittedWithTheUpdate()
    {
        var (service, invoiceRepo, supplierRepo, auditLogRepo) = CreateServiceWithAudit();
        var supplier = new Supplier { Name = "Test Supplier" };
        supplierRepo.Suppliers.Add(supplier);
        var created = await service.CreateAsync(new CreateInvoiceRequest(supplier.Id, "INV-1", null, null, "GBP", 100m, 20m, 120m, null));

        var result = await service.UpdateAsync(created.Value.Id, new UpdateInvoiceRequest(
            "INV-1", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Extracted));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(auditLogRepo.AuditLogs);
        Assert.Equal(AuditActions.InvoiceStatusChanged, entry.Action);
        Assert.Equal(nameof(Invoice), entry.EntityName);
        Assert.Equal(created.Value.Id, entry.EntityId);
        Assert.Equal(InvoiceStatusCodes.Received.ToString(), entry.PreviousValue); // CreateAsync always starts at Received
        Assert.Equal(InvoiceStatusCodes.Extracted.ToString(), entry.NewValue);

        // Staged, not independently saved - InvoiceService.UpdateAsync's own
        // SaveChangesAsync call is what commits it (see IAuditService.LogAsync's
        // doc comment); asserting it was never saved through the audit repository
        // itself proves the "commit together, not independently" design.
        Assert.False(auditLogRepo.SaveChangesCalled);
        Assert.True(invoiceRepo.SaveChangesCalled);
    }

    [Fact]
    public async Task UpdateAsync_StatusUnchanged_NoAuditLogEntryStaged()
    {
        var (service, _, supplierRepo, auditLogRepo) = CreateServiceWithAudit();
        var supplier = new Supplier { Name = "Test Supplier" };
        supplierRepo.Suppliers.Add(supplier);
        var created = await service.CreateAsync(new CreateInvoiceRequest(supplier.Id, "INV-1", null, null, "GBP", 100m, 20m, 120m, null));

        var result = await service.UpdateAsync(created.Value.Id, new UpdateInvoiceRequest(
            "INV-1-REV", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Received)); // same status as CreateAsync's default

        Assert.True(result.IsSuccess);
        Assert.Empty(auditLogRepo.AuditLogs);
    }

    [Fact]
    public async Task UpdateAsync_CheckedReadyToApproveToApproved_ApReviewerRole_Rejected_InvoiceUnchanged()
    {
        // WP-051 required scenario: a user holding only AP_REVIEWER cannot execute
        // the CHECKED_READY_TO_APPROVE -> APPROVED transition.
        var (service, invoiceRepo, currentUser, policyRepo) = CreateServiceWithApproval();
        SeedGbSkipsInvoiceApprovalPolicy(policyRepo);
        currentUser.RolesList.Add(Roles.ApReviewer);
        var invoice = await CreateInvoiceAtCheckedReadyToApproveAsync(service, invoiceRepo);

        var result = await service.UpdateAsync(invoice.Id, new UpdateInvoiceRequest(
            "INV-1", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Approved));

        Assert.True(result.IsFailure);
        Assert.Equal("Approval.Unauthorized", result.Error.Code);

        // The invoice is left completely untouched by the rejected attempt.
        var unchanged = invoiceRepo.Invoices.Single(i => i.Id == invoice.Id);
        Assert.Equal(InvoiceStatusCodes.CheckedReadyToApprove, unchanged.Status);
    }

    [Fact]
    public async Task UpdateAsync_CheckedReadyToApproveToApproved_FinanceManagerRole_Succeeds()
    {
        // WP-051 required scenario: a user holding FINANCE_MANAGER can execute the
        // CHECKED_READY_TO_APPROVE -> APPROVED transition.
        var (service, invoiceRepo, currentUser, policyRepo) = CreateServiceWithApproval();
        SeedGbSkipsInvoiceApprovalPolicy(policyRepo);
        currentUser.RolesList.Add(Roles.FinanceManager);
        var invoice = await CreateInvoiceAtCheckedReadyToApproveAsync(service, invoiceRepo);

        var result = await service.UpdateAsync(invoice.Id, new UpdateInvoiceRequest(
            "INV-1", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Approved));

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatusCodes.Approved, result.Value.Status);
    }

    [Fact]
    public async Task UpdateAsync_CheckedReadyToApproveToApproved_NoPolicyConfigured_FailsClosed()
    {
        // A domain with no ApprovalPolicy at all is NOT treated as "no restriction".
        var (service, invoiceRepo, currentUser, _) = CreateServiceWithApproval(); // policyRepo left empty
        currentUser.RolesList.Add(Roles.FinanceManager);
        var invoice = await CreateInvoiceAtCheckedReadyToApproveAsync(service, invoiceRepo);

        var result = await service.UpdateAsync(invoice.Id, new UpdateInvoiceRequest(
            "INV-1", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Approved));

        Assert.True(result.IsFailure);
        Assert.Equal("Approval.PolicyNotConfigured", result.Error.Code);
    }

    [Fact]
    public async Task UpdateAsync_OtherTransitions_NotGatedByApprovalPolicy()
    {
        // The role gate is narrow (WP-051 task 4) - it only applies to
        // CHECKED_READY_TO_APPROVE -> APPROVED. A transition to a DIFFERENT status
        // proceeds regardless of the acting user's roles or policy configuration.
        var (service, invoiceRepo, currentUser, _) = CreateServiceWithApproval(); // no policy seeded, no roles set
        var invoice = await CreateInvoiceAtCheckedReadyToApproveAsync(service, invoiceRepo);

        var result = await service.UpdateAsync(invoice.Id, new UpdateInvoiceRequest(
            "INV-1", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.NeedsQuery));

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatusCodes.NeedsQuery, result.Value.Status);
    }

    private static async Task<Invoice> CreateInvoiceAtCheckedReadyToApproveAsync(InvoiceService service, FakeInvoiceRepository invoiceRepo)
    {
        var invoice = new Invoice
        {
            SupplierId = Guid.NewGuid(),
            SupplierInvoiceNumber = "INV-1",
            Currency = "GBP",
            GrossTotal = 120m,
            Status = InvoiceStatusCodes.CheckedReadyToApprove,
        };
        invoiceRepo.Invoices.Add(invoice);
        await Task.CompletedTask;
        return invoice;
    }

    private static void SeedGbSkipsInvoiceApprovalPolicy(FakeApprovalPolicyRepository policyRepo) =>
        policyRepo.Policies.Add(new ApprovalPolicy
        {
            Domain = ApprovalDomains.InvoiceApproval,
            RequiredRole = Roles.FinanceManager,
            RequiresDualControl = false,
        });

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

        var result = await service.UpdateAsync(created.Value.Id, new UpdateInvoiceRequest(tooLong, null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Received));

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
        var (service, invoiceRepository, supplierRepository, _) = CreateServiceWithAudit();
        return (service, invoiceRepository, supplierRepository);
    }

    /// <summary>
    /// Same as <see cref="CreateService"/> but also exposes the
    /// <see cref="FakeAuditLogRepository"/> backing the real <see cref="AuditService"/>
    /// InvoiceService is wired to, for tests asserting on WP-013's automatic
    /// status-change audit logging specifically. Kept as a separate overload rather
    /// than changing CreateService's return shape, so the other 16+ pre-WP-013 tests
    /// in this file don't need updating for a dependency they don't care about.
    /// </summary>
    private static (InvoiceService Service, FakeInvoiceRepository InvoiceRepository, FakeSupplierRepository SupplierRepository, FakeAuditLogRepository AuditLogRepository) CreateServiceWithAudit()
    {
        var invoiceRepository = new FakeInvoiceRepository();
        var supplierRepository = new FakeSupplierRepository();
        var auditLogRepository = new FakeAuditLogRepository();
        var auditService = new AuditService(auditLogRepository, NullLogger<AuditService>.Instance);
        var currentUserService = new FakeCurrentUserService();
        var approvalAuthorizationService = new FakeApprovalAuthorizationService(); // defaults to always-authorized
        var service = new InvoiceService(
            invoiceRepository, supplierRepository, auditService, currentUserService, approvalAuthorizationService, NullLogger<InvoiceService>.Instance);
        return (service, invoiceRepository, supplierRepository, auditLogRepository);
    }

    /// <summary>
    /// Same as <see cref="CreateService"/> but also exposes the
    /// <see cref="FakeCurrentUserService"/> (to set the acting user's roles) and
    /// <see cref="FakeApprovalPolicyRepository"/> (to seed an ApprovalPolicy)
    /// backing InvoiceService (WP-051), for tests asserting on the
    /// CHECKED_READY_TO_APPROVE -&gt; APPROVED role gate specifically. Uses the REAL
    /// <see cref="ApprovalAuthorizationService"/> (not a fake of the whole
    /// service) so these tests prove the actual policy-checking logic, not just
    /// that InvoiceService reacts correctly to a mocked Result. A separate
    /// overload for the same reason as <see cref="CreateServiceWithAudit"/> - the
    /// other pre-WP-051 tests in this file don't need updating for dependencies
    /// they don't care about.
    /// </summary>
    private static (InvoiceService Service, FakeInvoiceRepository InvoiceRepository, FakeCurrentUserService CurrentUserService, FakeApprovalPolicyRepository ApprovalPolicyRepository) CreateServiceWithApproval()
    {
        var invoiceRepository = new FakeInvoiceRepository();
        var supplierRepository = new FakeSupplierRepository();
        var auditLogRepository = new FakeAuditLogRepository();
        var auditService = new AuditService(auditLogRepository, NullLogger<AuditService>.Instance);
        var currentUserService = new FakeCurrentUserService();
        var approvalPolicyRepository = new FakeApprovalPolicyRepository();
        var approvalAuthorizationService = new ApprovalAuthorizationService(approvalPolicyRepository);
        var service = new InvoiceService(
            invoiceRepository, supplierRepository, auditService, currentUserService, approvalAuthorizationService, NullLogger<InvoiceService>.Instance);
        return (service, invoiceRepository, currentUserService, approvalPolicyRepository);
    }
}
