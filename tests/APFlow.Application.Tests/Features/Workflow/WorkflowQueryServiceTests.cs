using APFlow.Application.Features.Workflow;
using APFlow.Application.Tests.Features.Workflow;
using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;
using Xunit;

namespace APFlow.Application.Tests.Features.Workflow;

public class WorkflowQueryServiceTests
{
    [Fact]
    public async Task GetActiveTemplateAsync_ReturnsStatusesSortedBySortOrder()
    {
        var repository = new FakeWorkflowTemplateRepository();
        var template = new WorkflowTemplate { DomainName = WorkflowDomains.Invoice, Name = "Platform Default", TenantId = null };
        template.Statuses.Add(new StatusReference { WorkflowTemplateId = template.Id, Code = "B", Name = "B", SortOrder = 20 });
        template.Statuses.Add(new StatusReference { WorkflowTemplateId = template.Id, Code = "A", Name = "A", SortOrder = 10 });
        repository.Templates.Add(template);
        var service = new WorkflowQueryService(repository);

        var result = await service.GetActiveTemplateAsync(WorkflowDomains.Invoice);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsTenantSpecific);
        Assert.Equal(["A", "B"], result.Value.Statuses.Select(s => s.Code));
    }

    [Fact]
    public async Task GetActiveTemplateAsync_TenantSpecificTemplate_IsTenantSpecificTrue()
    {
        var tenantId = Guid.NewGuid();
        var repository = new FakeWorkflowTemplateRepository { CurrentTenantId = tenantId };
        var template = new WorkflowTemplate { DomainName = WorkflowDomains.Invoice, Name = "GB Skips", TenantId = tenantId };
        repository.Templates.Add(template);
        var service = new WorkflowQueryService(repository);

        var result = await service.GetActiveTemplateAsync(WorkflowDomains.Invoice);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsTenantSpecific);
    }

    [Fact]
    public async Task GetActiveTemplateAsync_NoTemplateForDomain_ReturnsFailure()
    {
        var repository = new FakeWorkflowTemplateRepository();
        var service = new WorkflowQueryService(repository);

        var result = await service.GetActiveTemplateAsync("Unknown");

        Assert.True(result.IsFailure);
        Assert.Equal("Workflow.TemplateNotFound", result.Error.Code);
    }
}
