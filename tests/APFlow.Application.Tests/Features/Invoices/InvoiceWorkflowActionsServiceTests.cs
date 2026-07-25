using APFlow.Application.Features.Approval;
using APFlow.Application.Features.Invoices;
using APFlow.Application.Features.Workflow;
using APFlow.Application.Tests.Features;
using APFlow.Application.Tests.Features.Workflow;
using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;
using Xunit;

namespace APFlow.Application.Tests.Features.Invoices;

public class InvoiceWorkflowActionsServiceTests
{
    [Fact]
    public async Task GetAvailableActionsAsync_UnknownInvoice_ReturnsInvoiceNotFound()
    {
        var (service, _, _, _) = CreateService();

        var result = await service.GetAvailableActionsAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("Invoice.NotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetAvailableActionsAsync_NoTemplateConfigured_PropagatesWorkflowTemplateNotFound()
    {
        var (service, invoiceRepo, _, _) = CreateService(); // no template seeded
        var invoice = CreateInvoiceAtStatus(invoiceRepo, InvoiceStatusCodes.CheckedReadyToApprove);

        var result = await service.GetAvailableActionsAsync(invoice.Id);

        Assert.True(result.IsFailure);
        Assert.Equal("Workflow.TemplateNotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetAvailableActionsAsync_NoOutgoingTransitions_ReturnsEmptyList()
    {
        var (service, invoiceRepo, templateRepo, _) = CreateService();
        SeedTemplate(templateRepo); // seeds edges FROM CheckedReadyToApprove only
        var invoice = CreateInvoiceAtStatus(invoiceRepo, InvoiceStatusCodes.Archived); // a terminal status with no outgoing edges in this fixture

        var result = await service.GetAvailableActionsAsync(invoice.Id);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task GetAvailableActionsAsync_ApReviewer_OnlySeesUngatedTransition()
    {
        // WP-054 required scenario (half 1): AP_REVIEWER on an invoice with two
        // gated transitions (CheckedReadyToApprove -> Approved/NeedsQuery, per
        // the real RoleGatedTransitions.All pairs WP-053 defined) and one
        // ungated one (-> NeedsReviewFebina) should see only the ungated one.
        var invoiceRepo = new FakeInvoiceRepository();
        var templateRepo = new FakeWorkflowTemplateRepository();
        var policyRepo = new FakeApprovalPolicyRepository();
        SeedTemplate(templateRepo);
        SeedInvoiceApprovalPolicy(policyRepo, Roles.FinanceManager);
        var invoice = CreateInvoiceAtStatus(invoiceRepo, InvoiceStatusCodes.CheckedReadyToApprove);
        var currentUser = new FakeCurrentUserService();
        currentUser.RolesList.Add(Roles.ApReviewer);
        var actionsService = CreateActionsService(invoiceRepo, templateRepo, policyRepo, currentUser);

        var result = await actionsService.GetAvailableActionsAsync(invoice.Id);

        Assert.True(result.IsSuccess);
        var action = Assert.Single(result.Value);
        Assert.Equal(InvoiceStatusCodes.NeedsReviewFebina, action.TargetStatusCode);
        Assert.Equal("Needs Review by Febina", action.TargetStatusLabel);
    }

    [Fact]
    public async Task GetAvailableActionsAsync_FinanceManager_SeesAllThreeTransitions()
    {
        // WP-054 required scenario (half 2): the exact same invoice/template as
        // above, but for a FINANCE_MANAGER - who satisfies the gated transitions'
        // required role - all three transitions should be returned.
        var invoiceRepo = new FakeInvoiceRepository();
        var templateRepo = new FakeWorkflowTemplateRepository();
        var policyRepo = new FakeApprovalPolicyRepository();
        SeedTemplate(templateRepo);
        SeedInvoiceApprovalPolicy(policyRepo, Roles.FinanceManager);
        var invoice = CreateInvoiceAtStatus(invoiceRepo, InvoiceStatusCodes.CheckedReadyToApprove);
        var currentUser = new FakeCurrentUserService();
        currentUser.RolesList.Add(Roles.FinanceManager);
        var actionsService = CreateActionsService(invoiceRepo, templateRepo, policyRepo, currentUser);

        var result = await actionsService.GetAvailableActionsAsync(invoice.Id);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.Count);
        Assert.Contains(result.Value, a => a.TargetStatusCode == InvoiceStatusCodes.Approved && a.TargetStatusLabel == "Approved");
        Assert.Contains(result.Value, a => a.TargetStatusCode == InvoiceStatusCodes.NeedsQuery && a.TargetStatusLabel == "Needs Query");
        Assert.Contains(result.Value, a => a.TargetStatusCode == InvoiceStatusCodes.NeedsReviewFebina);
    }

    [Fact]
    public async Task GetAvailableActionsAsync_GatedTransition_NoApprovalPolicyConfigured_FailsClosed_OmitsGatedTransition()
    {
        // Fail-closed, same as ApprovalAuthorizationService's own contract: an
        // unconfigured policy is not treated as "no restriction" - the gated
        // transitions are simply omitted, not surfaced as an error, since the
        // ungated transition is still perfectly valid to return.
        var invoiceRepo = new FakeInvoiceRepository();
        var templateRepo = new FakeWorkflowTemplateRepository();
        SeedTemplate(templateRepo); // policy repository intentionally left empty
        var invoice = CreateInvoiceAtStatus(invoiceRepo, InvoiceStatusCodes.CheckedReadyToApprove);
        var currentUser = new FakeCurrentUserService();
        currentUser.RolesList.Add(Roles.FinanceManager);
        var actionsService = CreateActionsService(invoiceRepo, templateRepo, new FakeApprovalPolicyRepository(), currentUser);

        var result = await actionsService.GetAvailableActionsAsync(invoice.Id);

        Assert.True(result.IsSuccess);
        var action = Assert.Single(result.Value);
        Assert.Equal(InvoiceStatusCodes.NeedsReviewFebina, action.TargetStatusCode);
    }

    private static Invoice CreateInvoiceAtStatus(FakeInvoiceRepository invoiceRepo, string status)
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
        return invoice;
    }

    private static void SeedInvoiceApprovalPolicy(FakeApprovalPolicyRepository policyRepo, string requiredRole) =>
        policyRepo.Policies.Add(new ApprovalPolicy
        {
            Domain = ApprovalDomains.InvoiceApproval,
            RequiredRole = requiredRole,
            RequiresDualControl = false,
        });

    /// <summary>
    /// Seeds a minimal template covering only the edges these tests need,
    /// deliberately using the REAL <see cref="RoleGatedTransitions.All"/> pairs
    /// (two gated, plus one genuinely-ungated GB Skips edge) rather than an
    /// arbitrary fixture - <see cref="InvoiceWorkflowActionsService"/> calls the
    /// real, non-injectable <c>RoleGatedTransitions.RequiresApprovalRole</c>
    /// directly, so a fixture using invented pairs would not actually exercise
    /// gating.
    /// </summary>
    private static void SeedTemplate(FakeWorkflowTemplateRepository templateRepo)
    {
        var template = new WorkflowTemplate { DomainName = WorkflowDomains.Invoice, Name = "GB Skips", TenantId = null };

        template.Statuses.Add(new StatusReference { WorkflowTemplateId = template.Id, Code = InvoiceStatusCodes.CheckedReadyToApprove, Name = "Checked & Ready to Approve" });
        template.Statuses.Add(new StatusReference { WorkflowTemplateId = template.Id, Code = InvoiceStatusCodes.Approved, Name = "Approved" });
        template.Statuses.Add(new StatusReference { WorkflowTemplateId = template.Id, Code = InvoiceStatusCodes.NeedsQuery, Name = "Needs Query" });
        template.Statuses.Add(new StatusReference { WorkflowTemplateId = template.Id, Code = InvoiceStatusCodes.NeedsReviewFebina, Name = "Needs Review by Febina" });
        template.Statuses.Add(new StatusReference { WorkflowTemplateId = template.Id, Code = InvoiceStatusCodes.Archived, Name = "Archived" });

        template.Transitions.Add(new WorkflowTransition { WorkflowTemplateId = template.Id, FromStatusCode = InvoiceStatusCodes.CheckedReadyToApprove, ToStatusCode = InvoiceStatusCodes.Approved });
        template.Transitions.Add(new WorkflowTransition { WorkflowTemplateId = template.Id, FromStatusCode = InvoiceStatusCodes.CheckedReadyToApprove, ToStatusCode = InvoiceStatusCodes.NeedsQuery });
        template.Transitions.Add(new WorkflowTransition { WorkflowTemplateId = template.Id, FromStatusCode = InvoiceStatusCodes.CheckedReadyToApprove, ToStatusCode = InvoiceStatusCodes.NeedsReviewFebina });

        templateRepo.Templates.Add(template);
    }

    private static InvoiceWorkflowActionsService CreateActionsService(
        FakeInvoiceRepository invoiceRepository,
        FakeWorkflowTemplateRepository templateRepository,
        FakeApprovalPolicyRepository policyRepository,
        FakeCurrentUserService currentUserService) =>
        new(
            invoiceRepository,
            new WorkflowQueryService(templateRepository),
            new ApprovalAuthorizationService(policyRepository),
            currentUserService);

    private static (InvoiceWorkflowActionsService Service, FakeInvoiceRepository InvoiceRepository, FakeWorkflowTemplateRepository TemplateRepository, FakeApprovalPolicyRepository PolicyRepository) CreateService()
    {
        var invoiceRepository = new FakeInvoiceRepository();
        var templateRepository = new FakeWorkflowTemplateRepository();
        var policyRepository = new FakeApprovalPolicyRepository();
        var currentUserService = new FakeCurrentUserService();
        var service = CreateActionsService(invoiceRepository, templateRepository, policyRepository, currentUserService);
        return (service, invoiceRepository, templateRepository, policyRepository);
    }
}
