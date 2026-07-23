using APFlow.Application.Features.Workflow;
using APFlow.Application.Tests.Features.Workflow;
using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;
using Xunit;

namespace APFlow.Application.Tests.Features.Workflow;

public class WorkflowValidationServiceTests
{
    private static readonly Guid GbSkipsTenantId = Guid.NewGuid();

    [Fact]
    public async Task ValidateTransitionAsync_PlatformDefaultTenant_CheckedReadyToApprove_IsNotAValidStatus()
    {
        // WP-050 required scenario: the platform-default tenant cannot reach
        // GB Skips' tenant-specific statuses at all - they aren't even recognized
        // as valid statuses for that tenant's template, let alone reachable via a
        // transition.
        var (service, _) = CreateServiceWithPlatformDefaultOnly();

        var result = await service.ValidateTransitionAsync(
            WorkflowDomains.Invoice, InvoiceStatusCodes.AwaitingReview, InvoiceStatusCodes.CheckedReadyToApprove);

        Assert.True(result.IsFailure);
        Assert.Equal("Workflow.InvalidToStatus", result.Error.Code);
    }

    [Fact]
    public async Task ValidateTransitionAsync_PlatformDefaultTenant_NeedsReviewFebina_IsNotAValidStatus()
    {
        var (service, _) = CreateServiceWithPlatformDefaultOnly();

        var result = await service.ValidateTransitionAsync(
            WorkflowDomains.Invoice, InvoiceStatusCodes.AwaitingReview, InvoiceStatusCodes.NeedsReviewFebina);

        Assert.True(result.IsFailure);
        Assert.Equal("Workflow.InvalidToStatus", result.Error.Code);
    }

    [Fact]
    public async Task ValidateTransitionAsync_GbSkipsTenant_CheckedReadyToApprove_IsARecognizedStatus_ButTransitionNotYetAllowed()
    {
        // WP-050 required scenario: the GB Skips tenant CAN reach these statuses -
        // they are valid, recognized statuses in ITS template (unlike the platform
        // default) - proving the tenant-specific status list resolves correctly.
        // The ACTUAL transition is still rejected here because no WorkflowTransition
        // rows are seeded for either template (WP-050 task 4: not yet confirmed by
        // the Chief Technical Architect) - this is the mechanism correctly failing
        // closed on unconfirmed data, not a bug.
        var (service, _) = CreateServiceWithGbSkipsOnly(seedTransitions: false);

        var result = await service.ValidateTransitionAsync(
            WorkflowDomains.Invoice, InvoiceStatusCodes.AwaitingReview, InvoiceStatusCodes.CheckedReadyToApprove);

        Assert.True(result.IsFailure);
        // Specifically NOT "InvalidToStatus" - the status itself is valid for this
        // tenant, only the transition edge is missing.
        Assert.Equal("Workflow.TransitionNotAllowed", result.Error.Code);
    }

    [Fact]
    public async Task ValidateTransitionAsync_GbSkipsTenant_WithConfiguredTransition_IsAllowed()
    {
        // Proves the mechanism CAN allow a transition when data says so (not just
        // "always reject") - using explicit, test-constructed transition data, per
        // WP-050's own instruction not to rely on the unconfirmed real proposed set
        // for this proof.
        var (service, _) = CreateServiceWithGbSkipsOnly(seedTransitions: true);

        var result = await service.ValidateTransitionAsync(
            WorkflowDomains.Invoice, InvoiceStatusCodes.AwaitingReview, InvoiceStatusCodes.CheckedReadyToApprove);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ValidateTransitionAsync_PlatformDefaultTenant_UnconfiguredTransitionBetweenValidStatuses_Rejected()
    {
        // WP-050 required scenario: invalid transitions are rejected for the
        // platform-default tenant too, even between two individually-valid
        // statuses - since no transitions are seeded for the platform default
        // either (its own transition graph is not documented anywhere - see
        // docs/WP-050-Workflow-Engine-Decisions.md).
        var (service, _) = CreateServiceWithPlatformDefaultOnly();

        var result = await service.ValidateTransitionAsync(
            WorkflowDomains.Invoice, InvoiceStatusCodes.Received, InvoiceStatusCodes.Approved);

        Assert.True(result.IsFailure);
        Assert.Equal("Workflow.TransitionNotAllowed", result.Error.Code);
    }

    [Fact]
    public async Task ValidateTransitionAsync_GbSkipsTenant_UnconfiguredTransitionBetweenValidStatuses_Rejected()
    {
        // Same as above, but for a GB-Skips-specific pair that ISN'T the one
        // explicitly allowed in CreateServiceWithGbSkipsOnly's seeded data - proves
        // the mechanism isn't just "allow anything once any transition exists".
        var (service, _) = CreateServiceWithGbSkipsOnly(seedTransitions: true);

        var result = await service.ValidateTransitionAsync(
            WorkflowDomains.Invoice, InvoiceStatusCodes.NeedsReviewFebina, InvoiceStatusCodes.Approved);

        Assert.True(result.IsFailure);
        Assert.Equal("Workflow.TransitionNotAllowed", result.Error.Code);
    }

    [Fact]
    public async Task ValidateTransitionAsync_SameFromAndToStatus_AlwaysAllowed_NoTemplateLookupNeeded()
    {
        var repository = new FakeWorkflowTemplateRepository(); // deliberately empty - proves no lookup happens
        var service = new WorkflowValidationService(repository);

        var result = await service.ValidateTransitionAsync(WorkflowDomains.Invoice, InvoiceStatusCodes.Received, InvoiceStatusCodes.Received);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ValidateTransitionAsync_UnknownFromStatus_ReturnsFailure()
    {
        var (service, _) = CreateServiceWithPlatformDefaultOnly();

        var result = await service.ValidateTransitionAsync(WorkflowDomains.Invoice, "NOT_A_REAL_STATUS", InvoiceStatusCodes.Approved);

        Assert.True(result.IsFailure);
        Assert.Equal("Workflow.InvalidFromStatus", result.Error.Code);
    }

    [Fact]
    public async Task ValidateTransitionAsync_NoTemplateConfiguredForDomain_ReturnsFailure()
    {
        var repository = new FakeWorkflowTemplateRepository();
        var service = new WorkflowValidationService(repository);

        var result = await service.ValidateTransitionAsync("SomeUnknownDomain", InvoiceStatusCodes.Received, InvoiceStatusCodes.Approved);

        Assert.True(result.IsFailure);
        Assert.Equal("Workflow.TemplateNotFound", result.Error.Code);
    }

    private static (WorkflowValidationService Service, FakeWorkflowTemplateRepository Repository) CreateServiceWithPlatformDefaultOnly()
    {
        var repository = new FakeWorkflowTemplateRepository { CurrentTenantId = null };
        var template = new WorkflowTemplate { DomainName = WorkflowDomains.Invoice, Name = "Platform Default", TenantId = null };
        AddStatus(template, InvoiceStatusCodes.Received, 10);
        AddStatus(template, InvoiceStatusCodes.AwaitingReview, 40);
        AddStatus(template, InvoiceStatusCodes.Approved, 80);
        repository.Templates.Add(template);

        return (new WorkflowValidationService(repository), repository);
    }

    private static (WorkflowValidationService Service, FakeWorkflowTemplateRepository Repository) CreateServiceWithGbSkipsOnly(bool seedTransitions)
    {
        var repository = new FakeWorkflowTemplateRepository { CurrentTenantId = GbSkipsTenantId };
        var template = new WorkflowTemplate { DomainName = WorkflowDomains.Invoice, Name = "GB Skips", TenantId = GbSkipsTenantId };
        AddStatus(template, InvoiceStatusCodes.AwaitingReview, 40);
        AddStatus(template, InvoiceStatusCodes.CheckedReadyToApprove, 45);
        AddStatus(template, InvoiceStatusCodes.NeedsReviewFebina, 46);
        AddStatus(template, InvoiceStatusCodes.Approved, 80);

        if (seedTransitions)
        {
            template.Transitions.Add(new WorkflowTransition
            {
                WorkflowTemplateId = template.Id,
                FromStatusCode = InvoiceStatusCodes.AwaitingReview,
                ToStatusCode = InvoiceStatusCodes.CheckedReadyToApprove,
            });
        }

        repository.Templates.Add(template);

        return (new WorkflowValidationService(repository), repository);
    }

    private static void AddStatus(WorkflowTemplate template, string code, int sortOrder) =>
        template.Statuses.Add(new StatusReference
        {
            WorkflowTemplateId = template.Id,
            Code = code,
            Name = code,
            SortOrder = sortOrder,
        });
}
