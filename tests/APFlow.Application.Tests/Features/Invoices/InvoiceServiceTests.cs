using APFlow.Application.DTOs;
using APFlow.Application.Features.Approval;
using APFlow.Application.Features.Audit;
using APFlow.Application.Features.Invoices;
using APFlow.Application.Features.Workflow;
using APFlow.Application.Tests.Features;
using APFlow.Application.Tests.Features.Workflow;
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
            "INV-1-REV", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Approved, Notes: "Approved after review."));

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
            "INV-1", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Extracted, Notes: "Moved to extracted."));

        Assert.True(result.IsSuccess);
        // CreateAsync (WP-052 Part C) also stages its own InvoiceCreated entry, and
        // WP-084's now-mandatory note stages its own NoteAdded entry alongside the
        // status change - this test is about the status-change entry specifically,
        // so it filters to that one rather than assuming it's the only entry present.
        Assert.Equal(3, auditLogRepo.AuditLogs.Count);
        var entry = Assert.Single(auditLogRepo.AuditLogs, a => a.Action == AuditActions.InvoiceStatusChanged);
        Assert.Equal(nameof(Invoice), entry.EntityName);
        Assert.Equal(created.Value.Id, entry.EntityId);
        Assert.Equal(InvoiceStatusCodes.Received.ToString(), entry.PreviousValue); // CreateAsync always starts at Received
        Assert.Equal(InvoiceStatusCodes.Extracted.ToString(), entry.NewValue);

        // WP-084: the note is staged and committed atomically with the status
        // change, not as a separate, independently-failable follow-up call.
        var noteEntry = Assert.Single(auditLogRepo.AuditLogs, a => a.Action == AuditActions.NoteAdded);
        Assert.Equal("Moved to extracted.", noteEntry.NewValue);

        // Staged, not independently saved - InvoiceService.UpdateAsync's own
        // SaveChangesAsync call is what commits it (see IAuditService.LogAsync's
        // doc comment); asserting it was never saved through the audit repository
        // itself proves the "commit together, not independently" design.
        Assert.False(auditLogRepo.SaveChangesCalled);
        Assert.True(invoiceRepo.SaveChangesCalled);
    }

    [Fact]
    public async Task UpdateAsync_StatusUnchanged_NoAdditionalAuditLogEntryStaged()
    {
        var (service, _, supplierRepo, auditLogRepo) = CreateServiceWithAudit();
        var supplier = new Supplier { Name = "Test Supplier" };
        supplierRepo.Suppliers.Add(supplier);
        var created = await service.CreateAsync(new CreateInvoiceRequest(supplier.Id, "INV-1", null, null, "GBP", 100m, 20m, 120m, null));
        // CreateAsync itself already staged one InvoiceCreated entry (WP-052 Part
        // C) - this test asserts the UPDATE doesn't add a SECOND entry when the
        // status is unchanged, not that zero entries exist overall.
        Assert.Single(auditLogRepo.AuditLogs);

        var result = await service.UpdateAsync(created.Value.Id, new UpdateInvoiceRequest(
            "INV-1-REV", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Received)); // same status as CreateAsync's default

        Assert.True(result.IsSuccess);
        Assert.Single(auditLogRepo.AuditLogs);
    }

    [Fact]
    public async Task UpdateAsync_CheckedReadyToApproveToApproved_ApReviewerRole_Rejected_InvoiceUnchanged()
    {
        // WP-051/WP-053 required scenario: a user holding only AP_REVIEWER cannot
        // execute the CHECKED_READY_TO_APPROVE -> APPROVED transition.
        var (service, invoiceRepo, currentUser, policyRepo, templateRepo) = CreateServiceWithApproval();
        SeedGbSkipsTemplate(templateRepo);
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
        // WP-051/WP-053 required scenario: a user holding FINANCE_MANAGER can
        // execute the CHECKED_READY_TO_APPROVE -> APPROVED transition.
        var (service, invoiceRepo, currentUser, policyRepo, templateRepo) = CreateServiceWithApproval();
        SeedGbSkipsTemplate(templateRepo);
        SeedGbSkipsInvoiceApprovalPolicy(policyRepo);
        currentUser.RolesList.Add(Roles.FinanceManager);
        var invoice = await CreateInvoiceAtCheckedReadyToApproveAsync(service, invoiceRepo);

        var result = await service.UpdateAsync(invoice.Id, new UpdateInvoiceRequest(
            "INV-1", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Approved, Notes: "Approved by finance manager."));

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatusCodes.Approved, result.Value.Status);
    }

    [Fact]
    public async Task UpdateAsync_CheckedReadyToApproveToApproved_NoPolicyConfigured_FailsClosed()
    {
        // A domain with no ApprovalPolicy at all is NOT treated as "no restriction".
        var (service, invoiceRepo, currentUser, _, templateRepo) = CreateServiceWithApproval(); // policyRepo left empty
        SeedGbSkipsTemplate(templateRepo);
        currentUser.RolesList.Add(Roles.FinanceManager);
        var invoice = await CreateInvoiceAtCheckedReadyToApproveAsync(service, invoiceRepo);

        var result = await service.UpdateAsync(invoice.Id, new UpdateInvoiceRequest(
            "INV-1", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Approved));

        Assert.True(result.IsFailure);
        Assert.Equal("Approval.PolicyNotConfigured", result.Error.Code);
    }

    [Theory]
    [InlineData(InvoiceStatusCodes.CheckedReadyToApprove, InvoiceStatusCodes.Approved)]
    [InlineData(InvoiceStatusCodes.CheckedReadyToApprove, InvoiceStatusCodes.NeedsQuery)]
    [InlineData(InvoiceStatusCodes.Rejected, InvoiceStatusCodes.AwaitingReview)]
    [InlineData(InvoiceStatusCodes.Cancelled, InvoiceStatusCodes.Received)]
    [InlineData(InvoiceStatusCodes.NeedsReviewFebina, InvoiceStatusCodes.CheckedReadyToApprove)]
    [InlineData(InvoiceStatusCodes.NeedsReviewFebina, InvoiceStatusCodes.NeedsQuery)]
    [InlineData(InvoiceStatusCodes.NeedsReviewFebina, InvoiceStatusCodes.Rejected)]
    public async Task UpdateAsync_RoleGatedTransition_ApReviewerRole_Rejected(string fromStatus, string toStatus)
    {
        // WP-053 required scenario, extended 2026-08-04 to the three
        // NEEDS_REVIEW_FEBINA resolution edges (see RoleGatedTransitions' own doc
        // comment): AP_REVIEWER cannot execute ANY of the seven role-gated
        // transitions - not just the original approval one WP-051 gated.
        var (service, invoiceRepo, currentUser, policyRepo, templateRepo) = CreateServiceWithApproval();
        SeedGbSkipsTemplate(templateRepo);
        SeedGbSkipsInvoiceApprovalPolicy(policyRepo);
        currentUser.RolesList.Add(Roles.ApReviewer);
        var invoice = await CreateInvoiceAtStatusAsync(invoiceRepo, fromStatus);

        var result = await service.UpdateAsync(invoice.Id, new UpdateInvoiceRequest(
            "INV-1", null, null, "GBP", 100m, 20m, 120m, toStatus));

        Assert.True(result.IsFailure);
        Assert.Equal("Approval.Unauthorized", result.Error.Code);
        Assert.Equal(fromStatus, invoiceRepo.Invoices.Single(i => i.Id == invoice.Id).Status);
    }

    [Theory]
    [InlineData(InvoiceStatusCodes.CheckedReadyToApprove, InvoiceStatusCodes.Approved)]
    [InlineData(InvoiceStatusCodes.CheckedReadyToApprove, InvoiceStatusCodes.NeedsQuery)]
    [InlineData(InvoiceStatusCodes.Rejected, InvoiceStatusCodes.AwaitingReview)]
    [InlineData(InvoiceStatusCodes.Cancelled, InvoiceStatusCodes.Received)]
    [InlineData(InvoiceStatusCodes.NeedsReviewFebina, InvoiceStatusCodes.CheckedReadyToApprove)]
    [InlineData(InvoiceStatusCodes.NeedsReviewFebina, InvoiceStatusCodes.NeedsQuery)]
    [InlineData(InvoiceStatusCodes.NeedsReviewFebina, InvoiceStatusCodes.Rejected)]
    public async Task UpdateAsync_RoleGatedTransition_FinanceManagerRole_Succeeds(string fromStatus, string toStatus)
    {
        // WP-053 required scenario, extended 2026-08-04: FINANCE_MANAGER can execute
        // all seven role-gated transitions.
        var (service, invoiceRepo, currentUser, policyRepo, templateRepo) = CreateServiceWithApproval();
        SeedGbSkipsTemplate(templateRepo);
        SeedGbSkipsInvoiceApprovalPolicy(policyRepo);
        currentUser.RolesList.Add(Roles.FinanceManager);
        var invoice = await CreateInvoiceAtStatusAsync(invoiceRepo, fromStatus);

        var result = await service.UpdateAsync(invoice.Id, new UpdateInvoiceRequest(
            "INV-1", null, null, "GBP", 100m, 20m, 120m, toStatus, Notes: "Transition note."));

        Assert.True(result.IsSuccess);
        Assert.Equal(toStatus, result.Value.Status);
    }

    [Fact]
    public async Task UpdateAsync_AwaitingReviewToApproved_PermittedForPlatformDefaultTemplate()
    {
        // WP-053 required scenario (half 1): the platform-default template DOES
        // allow direct reviewer approval - no CHECKED_READY_TO_APPROVE step, and no
        // role gate on this edge.
        var (service, invoiceRepo, currentUser, _, templateRepo) = CreateServiceWithApproval();
        SeedPlatformDefaultTemplate(templateRepo);
        currentUser.RolesList.Add(Roles.ApReviewer); // deliberately NOT FinanceManager
        var invoice = await CreateInvoiceAtStatusAsync(invoiceRepo, InvoiceStatusCodes.AwaitingReview);

        var result = await service.UpdateAsync(invoice.Id, new UpdateInvoiceRequest(
            "INV-1", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Approved, Notes: "Approved directly (platform default)."));

        Assert.True(result.IsSuccess);
        Assert.Equal(InvoiceStatusCodes.Approved, result.Value.Status);
    }

    [Fact]
    public async Task UpdateAsync_AwaitingReviewToApproved_RejectedForGbSkipsTemplate()
    {
        // WP-053 required scenario (half 2): GB Skips' template deliberately REMOVES
        // the direct AWAITING_REVIEW -> APPROVED edge - approval must route through
        // CHECKED_READY_TO_APPROVE. Rejected as a transition (not a role) failure,
        // even for a FINANCE_MANAGER, because the edge simply doesn't exist.
        var (service, invoiceRepo, currentUser, policyRepo, templateRepo) = CreateServiceWithApproval();
        SeedGbSkipsTemplate(templateRepo);
        SeedGbSkipsInvoiceApprovalPolicy(policyRepo);
        currentUser.RolesList.Add(Roles.FinanceManager);
        var invoice = await CreateInvoiceAtStatusAsync(invoiceRepo, InvoiceStatusCodes.AwaitingReview);

        var result = await service.UpdateAsync(invoice.Id, new UpdateInvoiceRequest(
            "INV-1", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Approved));

        Assert.True(result.IsFailure);
        Assert.Equal("Workflow.TransitionNotAllowed", result.Error.Code);
        Assert.Equal(InvoiceStatusCodes.AwaitingReview, invoiceRepo.Invoices.Single(i => i.Id == invoice.Id).Status);
    }

    [Fact]
    public async Task UpdateAsync_TransitionNotInGraph_Rejected()
    {
        // Enforcement is genuinely live: an edge absent from the confirmed graph is
        // rejected, not silently allowed as it was before WP-053.
        var (service, invoiceRepo, currentUser, _, templateRepo) = CreateServiceWithApproval();
        SeedPlatformDefaultTemplate(templateRepo);
        currentUser.RolesList.Add(Roles.FinanceManager);
        var invoice = await CreateInvoiceAtStatusAsync(invoiceRepo, InvoiceStatusCodes.Received);

        // RECEIVED -> PAID is nowhere in the confirmed graph.
        var result = await service.UpdateAsync(invoice.Id, new UpdateInvoiceRequest(
            "INV-1", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Paid));

        Assert.True(result.IsFailure);
        Assert.Equal("Workflow.TransitionNotAllowed", result.Error.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_GatedTransition_MissingOrEmptyNote_Rejected_InvoiceUnchanged(string? notes)
    {
        // WP-084: an otherwise-valid, otherwise-authorized GATED transition
        // (CHECKED_READY_TO_APPROVE -> APPROVED, role-gated per RoleGatedTransitions)
        // is still rejected if the note is missing, empty, or whitespace-only - the
        // acting user genuinely holds FINANCE_MANAGER here, so this proves the note
        // requirement is a real, independent check, not just incidentally passing
        // because the role gate already failed first.
        var (service, invoiceRepo, currentUser, policyRepo, templateRepo) = CreateServiceWithApproval();
        SeedGbSkipsTemplate(templateRepo);
        SeedGbSkipsInvoiceApprovalPolicy(policyRepo);
        currentUser.RolesList.Add(Roles.FinanceManager);
        var invoice = await CreateInvoiceAtCheckedReadyToApproveAsync(service, invoiceRepo);

        var result = await service.UpdateAsync(invoice.Id, new UpdateInvoiceRequest(
            "INV-1", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Approved, Notes: notes));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoice.NoteRequiredForTransition", result.Error.Code);

        var unchanged = invoiceRepo.Invoices.Single(i => i.Id == invoice.Id);
        Assert.Equal(InvoiceStatusCodes.CheckedReadyToApprove, unchanged.Status);
        Assert.Empty(unchanged.Notes);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_UngatedTransition_MissingOrEmptyNote_Rejected_InvoiceUnchanged(string? notes)
    {
        // WP-084: applies to EVERY human-initiated transition, not just the
        // role-gated ones - RECEIVED -> PROCESSING carries no role gate at all
        // (RoleGatedTransitions doesn't cover it) and is still rejected without a
        // note.
        var (service, invoiceRepo, _, _, templateRepo) = CreateServiceWithApproval();
        SeedPlatformDefaultTemplate(templateRepo);
        var invoice = await CreateInvoiceAtStatusAsync(invoiceRepo, InvoiceStatusCodes.Received);

        var result = await service.UpdateAsync(invoice.Id, new UpdateInvoiceRequest(
            "INV-1", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.Processing, Notes: notes));

        Assert.True(result.IsFailure);
        Assert.Equal("Invoice.NoteRequiredForTransition", result.Error.Code);

        var unchanged = invoiceRepo.Invoices.Single(i => i.Id == invoice.Id);
        Assert.Equal(InvoiceStatusCodes.Received, unchanged.Status);
        Assert.Empty(unchanged.Notes);
    }

    [Fact]
    public async Task UpdateAsync_NonStatusFieldEditWithUnchangedStatus_NotValidatedOrGated()
    {
        // A plain field edit is not a transition - it must not be blocked by
        // enforcement, even with no template seeded at all.
        var (service, invoiceRepo, _, _, _) = CreateServiceWithApproval(); // no template, no policy, no roles
        var invoice = await CreateInvoiceAtStatusAsync(invoiceRepo, InvoiceStatusCodes.AwaitingReview);

        var result = await service.UpdateAsync(invoice.Id, new UpdateInvoiceRequest(
            "INV-1-REVISED", null, null, "GBP", 100m, 20m, 120m, InvoiceStatusCodes.AwaitingReview));

        Assert.True(result.IsSuccess);
        Assert.Equal("INV-1-REVISED", result.Value.SupplierInvoiceNumber);
    }

    private static async Task<Invoice> CreateInvoiceAtCheckedReadyToApproveAsync(InvoiceService service, FakeInvoiceRepository invoiceRepo) =>
        await CreateInvoiceAtStatusAsync(invoiceRepo, InvoiceStatusCodes.CheckedReadyToApprove);

    private static Task<Invoice> CreateInvoiceAtStatusAsync(FakeInvoiceRepository invoiceRepo, string status)
    {
        var invoice = new Invoice
        {
            SupplierId = Guid.NewGuid(),
            SupplierInvoiceNumber = "INV-1",
            Currency = "GBP",
            GrossTotal = 120m,
            Status = status,
        };
        invoiceRepo.Invoices.Add(invoice);
        return Task.FromResult(invoice);
    }

    private static void SeedGbSkipsInvoiceApprovalPolicy(FakeApprovalPolicyRepository policyRepo) =>
        policyRepo.Policies.Add(new ApprovalPolicy
        {
            Domain = ApprovalDomains.InvoiceApproval,
            RequiredRole = Roles.FinanceManager,
            RequiresDualControl = false,
        });

    /// <summary>
    /// Seeds the platform-default template with the real WP-053 confirmed graph -
    /// deliberately mirroring the actual seeded transitions rather than a
    /// hand-invented subset, so these tests exercise the graph that genuinely
    /// ships. Kept in sync with WorkflowTransitionSeedData by
    /// WorkflowTransitionSeedDataTests (APFlow.Infrastructure.Tests), which asserts
    /// against the same expected edges from the Infrastructure side.
    /// </summary>
    private static void SeedPlatformDefaultTemplate(FakeWorkflowTemplateRepository templateRepo) =>
        SeedTemplate(templateRepo, tenantId: null, includeGbSkipsExtras: false);

    private static void SeedGbSkipsTemplate(FakeWorkflowTemplateRepository templateRepo) =>
        SeedTemplate(templateRepo, tenantId: Guid.NewGuid(), includeGbSkipsExtras: true);

    private static void SeedTemplate(FakeWorkflowTemplateRepository templateRepo, Guid? tenantId, bool includeGbSkipsExtras)
    {
        var template = new WorkflowTemplate
        {
            DomainName = WorkflowDomains.Invoice,
            Name = includeGbSkipsExtras ? "GB Skips" : "Platform Default",
            TenantId = tenantId,
        };
        templateRepo.CurrentTenantId = tenantId;

        string[] statuses =
        [
            InvoiceStatusCodes.Received, InvoiceStatusCodes.Processing, InvoiceStatusCodes.Extracted,
            InvoiceStatusCodes.AwaitingReview, InvoiceStatusCodes.NeedsQuery, InvoiceStatusCodes.QueryRaised,
            InvoiceStatusCodes.AwaitingSupplierResponse, InvoiceStatusCodes.Approved, InvoiceStatusCodes.Rejected,
            InvoiceStatusCodes.Cancelled, InvoiceStatusCodes.ReadyForPayment, InvoiceStatusCodes.Paid,
            InvoiceStatusCodes.Archived,
        ];

        foreach (var code in statuses)
        {
            template.Statuses.Add(new StatusReference { WorkflowTemplateId = template.Id, Code = code, Name = code });
        }

        (string From, string To)[] shared =
        [
            (InvoiceStatusCodes.Received, InvoiceStatusCodes.Processing),
            (InvoiceStatusCodes.Processing, InvoiceStatusCodes.Extracted),
            (InvoiceStatusCodes.Extracted, InvoiceStatusCodes.AwaitingReview),
            (InvoiceStatusCodes.AwaitingReview, InvoiceStatusCodes.NeedsQuery),
            (InvoiceStatusCodes.NeedsQuery, InvoiceStatusCodes.QueryRaised),
            (InvoiceStatusCodes.QueryRaised, InvoiceStatusCodes.AwaitingSupplierResponse),
            (InvoiceStatusCodes.AwaitingSupplierResponse, InvoiceStatusCodes.AwaitingReview),
            (InvoiceStatusCodes.AwaitingReview, InvoiceStatusCodes.Rejected),
            (InvoiceStatusCodes.QueryRaised, InvoiceStatusCodes.Rejected),
            (InvoiceStatusCodes.AwaitingSupplierResponse, InvoiceStatusCodes.Rejected),
            (InvoiceStatusCodes.Received, InvoiceStatusCodes.Cancelled),
            (InvoiceStatusCodes.Processing, InvoiceStatusCodes.Cancelled),
            (InvoiceStatusCodes.Extracted, InvoiceStatusCodes.Cancelled),
            (InvoiceStatusCodes.AwaitingReview, InvoiceStatusCodes.Cancelled),
            (InvoiceStatusCodes.NeedsQuery, InvoiceStatusCodes.Cancelled),
            (InvoiceStatusCodes.QueryRaised, InvoiceStatusCodes.Cancelled),
            (InvoiceStatusCodes.AwaitingSupplierResponse, InvoiceStatusCodes.Cancelled),
            (InvoiceStatusCodes.Rejected, InvoiceStatusCodes.AwaitingReview),
            (InvoiceStatusCodes.Cancelled, InvoiceStatusCodes.Received),
            (InvoiceStatusCodes.Approved, InvoiceStatusCodes.ReadyForPayment),
            (InvoiceStatusCodes.ReadyForPayment, InvoiceStatusCodes.Paid),
            (InvoiceStatusCodes.Paid, InvoiceStatusCodes.Archived),
            (InvoiceStatusCodes.Rejected, InvoiceStatusCodes.Archived),
            (InvoiceStatusCodes.Cancelled, InvoiceStatusCodes.Archived),
        ];

        foreach (var (from, to) in shared)
        {
            template.Transitions.Add(new WorkflowTransition { WorkflowTemplateId = template.Id, FromStatusCode = from, ToStatusCode = to });
        }

        if (includeGbSkipsExtras)
        {
            template.Statuses.Add(new StatusReference { WorkflowTemplateId = template.Id, Code = InvoiceStatusCodes.CheckedReadyToApprove, Name = "Checked & Ready to Approve" });
            template.Statuses.Add(new StatusReference { WorkflowTemplateId = template.Id, Code = InvoiceStatusCodes.NeedsReviewFebina, Name = "Needs Review by Febina" });

            (string From, string To)[] gbSkipsExtras =
            [
                (InvoiceStatusCodes.AwaitingReview, InvoiceStatusCodes.CheckedReadyToApprove),
                (InvoiceStatusCodes.CheckedReadyToApprove, InvoiceStatusCodes.Approved),
                (InvoiceStatusCodes.AwaitingReview, InvoiceStatusCodes.NeedsReviewFebina),
                (InvoiceStatusCodes.CheckedReadyToApprove, InvoiceStatusCodes.NeedsReviewFebina),
                (InvoiceStatusCodes.NeedsReviewFebina, InvoiceStatusCodes.CheckedReadyToApprove),
                (InvoiceStatusCodes.NeedsReviewFebina, InvoiceStatusCodes.NeedsQuery),
                (InvoiceStatusCodes.NeedsReviewFebina, InvoiceStatusCodes.Rejected),
                (InvoiceStatusCodes.CheckedReadyToApprove, InvoiceStatusCodes.NeedsQuery),
            ];

            foreach (var (from, to) in gbSkipsExtras)
            {
                template.Transitions.Add(new WorkflowTransition { WorkflowTemplateId = template.Id, FromStatusCode = from, ToStatusCode = to });
            }
        }
        else
        {
            // Platform-default only: direct reviewer approval.
            template.Transitions.Add(new WorkflowTransition
            {
                WorkflowTemplateId = template.Id,
                FromStatusCode = InvoiceStatusCodes.AwaitingReview,
                ToStatusCode = InvoiceStatusCodes.Approved,
            });
        }

        templateRepo.Templates.Add(template);
    }

    [Fact]
    public async Task CreateAsync_StagesInvoiceCreatedAuditEntry_CommittedWithTheInsert()
    {
        var (service, invoiceRepo, supplierRepo, auditLogRepo) = CreateServiceWithAudit();
        var supplier = new Supplier { Name = "Test Supplier" };
        supplierRepo.Suppliers.Add(supplier);

        var result = await service.CreateAsync(new CreateInvoiceRequest(supplier.Id, "INV-1", new DateOnly(2026, 1, 1), null, "GBP", 100m, 20m, 120m, null));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(auditLogRepo.AuditLogs);
        Assert.Equal(AuditActions.InvoiceCreated, entry.Action);
        Assert.Equal(nameof(Invoice), entry.EntityName);
        Assert.Equal(result.Value.Id, entry.EntityId);
        Assert.Null(entry.PreviousValue);
        Assert.NotNull(entry.NewValue);
        Assert.Contains(supplier.Id.ToString(), entry.NewValue);
        Assert.Contains("Test Supplier", entry.NewValue);
        Assert.Contains("INV-1", entry.NewValue);
        Assert.Contains("120", entry.NewValue); // GrossTotal
        Assert.Contains(InvoiceStatusCodes.Received, entry.NewValue); // initial status

        // Staged, not independently saved - same "commit together" design as the
        // status-change entry.
        Assert.False(auditLogRepo.SaveChangesCalled);
        Assert.True(invoiceRepo.SaveChangesCalled);
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
    public async Task DeleteAsync_StagesInvoiceDeletedAuditEntry_WithPreDeletionSnapshot()
    {
        var (service, invoiceRepo, supplierRepo, auditLogRepo) = CreateServiceWithAudit();
        var supplier = new Supplier { Name = "Test Supplier" };
        supplierRepo.Suppliers.Add(supplier);
        var created = await service.CreateAsync(new CreateInvoiceRequest(supplier.Id, "INV-1", null, null, "GBP", 100m, 20m, 120m, null));
        auditLogRepo.AuditLogs.Clear(); // isolate this test to the delete-specific entry

        var result = await service.DeleteAsync(created.Value.Id);

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(auditLogRepo.AuditLogs);
        Assert.Equal(AuditActions.InvoiceDeleted, entry.Action);
        Assert.Equal(nameof(Invoice), entry.EntityName);
        Assert.Equal(created.Value.Id, entry.EntityId);
        Assert.Null(entry.NewValue);
        Assert.NotNull(entry.PreviousValue);
        Assert.Contains("INV-1", entry.PreviousValue);
        Assert.Contains("120", entry.PreviousValue);

        Assert.False(auditLogRepo.SaveChangesCalled);
        Assert.True(invoiceRepo.SaveChangesCalled);
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
    public async Task AddNoteAsync_StagesNoteAddedAuditEntry_WithFullNoteContentAsNewValue()
    {
        var (service, invoiceRepo, supplierRepo, auditLogRepo) = CreateServiceWithAudit();
        var supplier = new Supplier { Name = "Test Supplier" };
        supplierRepo.Suppliers.Add(supplier);
        var created = await service.CreateAsync(new CreateInvoiceRequest(supplier.Id, "INV-1", null, null, "GBP", 100m, 20m, 120m, null));
        auditLogRepo.AuditLogs.Clear(); // isolate this test to the note-specific entry

        var result = await service.AddNoteAsync(created.Value.Id, "Looks correct, approved.");

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(auditLogRepo.AuditLogs);
        Assert.Equal(AuditActions.NoteAdded, entry.Action);
        Assert.Equal(nameof(Invoice), entry.EntityName);
        Assert.Equal(created.Value.Id, entry.EntityId);
        Assert.Null(entry.PreviousValue);
        Assert.Equal("Looks correct, approved.", entry.NewValue); // the full, raw note content - not JSON

        Assert.False(auditLogRepo.SaveChangesCalled);
        Assert.True(invoiceRepo.SaveChangesCalled);
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
    public async Task AddNoteAsync_ReturnsCreatedNote_WithAuthorDisplayNameFromCurrentUser()
    {
        // WP-055: AuthorDisplayName is captured from ICurrentUserService.DisplayName
        // at creation time - not left null, not derived from CreatedBy (a stable
        // id, not a display-friendly name - see InvoiceNote.AuthorDisplayName's
        // own doc comment).
        var (service, invoiceRepo, currentUser, _, _) = CreateServiceWithApproval();
        currentUser.DisplayName = "Priya Shah";
        var invoice = await CreateInvoiceAtStatusAsync(invoiceRepo, InvoiceStatusCodes.AwaitingReview);

        var result = await service.AddNoteAsync(invoice.Id, "Looks correct, approved.");

        Assert.True(result.IsSuccess);
        Assert.NotEqual(Guid.Empty, result.Value.Id);
        Assert.Equal("Looks correct, approved.", result.Value.Content);
        Assert.Equal("Priya Shah", result.Value.AuthorDisplayName);
    }

    [Fact]
    public async Task AddNoteAsync_NoDisplayNameClaimOnToken_AuthorDisplayNameIsNull()
    {
        // Not every validated token is guaranteed to carry a name claim (see
        // CurrentUserService.DisplayName's own doc comment) - this must not throw
        // or substitute some other identifier, just leave it null.
        var (service, invoiceRepo, currentUser, _, _) = CreateServiceWithApproval(); // DisplayName defaults to null
        var invoice = await CreateInvoiceAtStatusAsync(invoiceRepo, InvoiceStatusCodes.AwaitingReview);

        var result = await service.AddNoteAsync(invoice.Id, "A note with no display name available.");

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.AuthorDisplayName);
    }

    [Fact]
    public async Task GetNotesAsync_ReturnsPreviouslyAddedNotes()
    {
        // Ordering (most recent first) relies on CreatedAtUtc, which only a real
        // AppDbContext's SaveChanges stamps (see AuditEntity's own doc comment) -
        // FakeInvoiceRepository does not, so this test proves presence/shape only.
        // The real EF Core ordering is proven against a real AppDbContext in
        // InvoiceNoteRepositoryTests (APFlow.Infrastructure.Tests).
        var (service, invoiceRepo, currentUser, _, _) = CreateServiceWithApproval();
        currentUser.DisplayName = "Priya Shah";
        var invoice = await CreateInvoiceAtStatusAsync(invoiceRepo, InvoiceStatusCodes.AwaitingReview);
        await service.AddNoteAsync(invoice.Id, "First note.");
        await service.AddNoteAsync(invoice.Id, "Second note.");

        var result = await service.GetNotesAsync(invoice.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, n => n.Content == "First note." && n.AuthorDisplayName == "Priya Shah");
        Assert.Contains(result.Value, n => n.Content == "Second note." && n.AuthorDisplayName == "Priya Shah");
    }

    [Fact]
    public async Task GetNotesAsync_MissingInvoice_ReturnsFailure()
    {
        var (service, _, _) = CreateService();

        var result = await service.GetNotesAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("Invoice.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetExtractedFieldsAsync_ReturnsFieldsInCanonicalOrder()
    {
        // WP-056: EF Core/SQL row order is not guaranteed, so
        // GetExtractedFieldsAsync must sort explicitly - assert against fields
        // added in a deliberately scrambled order to actually prove the sort,
        // not just happen to pass because insertion order was already correct.
        var (service, invoiceRepo, _) = CreateService();
        var invoice = await CreateInvoiceAtStatusAsync(invoiceRepo, InvoiceStatusCodes.Extracted);
        invoice.ExtractedFields.Add(new InvoiceExtractedField { InvoiceId = invoice.Id, FieldKey = InvoiceExtractedFieldKeys.GrossTotal, Label = "Gross Total", Value = "120", ConfidenceScore = 0.9 });
        invoice.ExtractedFields.Add(new InvoiceExtractedField { InvoiceId = invoice.Id, FieldKey = InvoiceExtractedFieldKeys.SupplierName, Label = "Supplier Name", Value = "Acme Ltd", ConfidenceScore = 0.95 });
        invoice.ExtractedFields.Add(new InvoiceExtractedField { InvoiceId = invoice.Id, FieldKey = InvoiceExtractedFieldKeys.InvoiceDate, Label = "Invoice Date", Value = "2026-01-01", ConfidenceScore = 0.88 });

        var result = await service.GetExtractedFieldsAsync(invoice.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [InvoiceExtractedFieldKeys.SupplierName, InvoiceExtractedFieldKeys.InvoiceDate, InvoiceExtractedFieldKeys.GrossTotal],
            result.Value.Select(f => f.FieldKey));
    }

    [Fact]
    public async Task GetExtractedFieldsAsync_NoExtractedFields_ReturnsEmptyList_NotAFailure()
    {
        // Normal for an invoice created before WP-056, or not processed via the
        // WP-012 pipeline at all (e.g. created manually) - not an error.
        var (service, invoiceRepo, _) = CreateService();
        var invoice = await CreateInvoiceAtStatusAsync(invoiceRepo, InvoiceStatusCodes.Received);

        var result = await service.GetExtractedFieldsAsync(invoice.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GetExtractedFieldsAsync_MissingInvoice_ReturnsFailure()
    {
        var (service, _, _) = CreateService();

        var result = await service.GetExtractedFieldsAsync(Guid.NewGuid());

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
            invoiceRepository, supplierRepository, auditService, currentUserService, approvalAuthorizationService,
            new FakeWorkflowValidationService(), NullLogger<InvoiceService>.Instance); // permissive: these tests predate WP-053 and aren't about transition validation
        return (service, invoiceRepository, supplierRepository, auditLogRepository);
    }

    /// <summary>
    /// Same as <see cref="CreateService"/> but also exposes the
    /// <see cref="FakeCurrentUserService"/> (to set the acting user's roles),
    /// <see cref="FakeApprovalPolicyRepository"/> (to seed an ApprovalPolicy), and
    /// <see cref="FakeWorkflowTemplateRepository"/> (to seed a transition graph)
    /// backing InvoiceService - for WP-051/WP-053 tests asserting on transition
    /// validation and role gating. Uses the REAL
    /// <see cref="ApprovalAuthorizationService"/> and REAL
    /// <see cref="WorkflowValidationService"/> (not fakes of the whole services) so
    /// these tests prove the actual policy/transition-checking logic, not just that
    /// InvoiceService reacts correctly to a mocked Result.
    /// </summary>
    private static (InvoiceService Service, FakeInvoiceRepository InvoiceRepository, FakeCurrentUserService CurrentUserService, FakeApprovalPolicyRepository ApprovalPolicyRepository, FakeWorkflowTemplateRepository WorkflowTemplateRepository) CreateServiceWithApproval()
    {
        var invoiceRepository = new FakeInvoiceRepository();
        var supplierRepository = new FakeSupplierRepository();
        var auditLogRepository = new FakeAuditLogRepository();
        var auditService = new AuditService(auditLogRepository, NullLogger<AuditService>.Instance);
        var currentUserService = new FakeCurrentUserService();
        var approvalPolicyRepository = new FakeApprovalPolicyRepository();
        var approvalAuthorizationService = new ApprovalAuthorizationService(approvalPolicyRepository);
        var workflowTemplateRepository = new FakeWorkflowTemplateRepository();
        var workflowValidationService = new WorkflowValidationService(workflowTemplateRepository);
        var service = new InvoiceService(
            invoiceRepository, supplierRepository, auditService, currentUserService, approvalAuthorizationService,
            workflowValidationService, NullLogger<InvoiceService>.Instance);
        return (service, invoiceRepository, currentUserService, approvalPolicyRepository, workflowTemplateRepository);
    }
}
