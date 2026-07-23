using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Entities;

namespace APFlow.Application.Features.Workflow;

/// <summary>
/// Default implementation of <see cref="IWorkflowQueryService"/>. Depends only on
/// <see cref="IWorkflowTemplateRepository"/>, so this class is fully unit-testable
/// with a fake repository.
/// </summary>
public sealed class WorkflowQueryService : IWorkflowQueryService
{
    private readonly IWorkflowTemplateRepository _repository;

    /// <summary>Creates a new <see cref="WorkflowQueryService"/>.</summary>
    public WorkflowQueryService(IWorkflowTemplateRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<Result<WorkflowTemplateDto>> GetActiveTemplateAsync(string domainName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domainName))
        {
            return Result.Failure<WorkflowTemplateDto>(new Error("Workflow.InvalidDomainName", "DomainName must not be empty."));
        }

        var template = await _repository.GetActiveTemplateAsync(domainName, cancellationToken);
        if (template is null)
        {
            return Result.Failure<WorkflowTemplateDto>(
                new Error("Workflow.TemplateNotFound", $"No workflow template (platform-default or tenant-specific) is configured for domain '{domainName}'."));
        }

        return Result.Success(ToDto(template));
    }

    private static WorkflowTemplateDto ToDto(WorkflowTemplate template) => new(
        Id: template.Id,
        DomainName: template.DomainName,
        Name: template.Name,
        IsTenantSpecific: template.TenantId is not null,
        Statuses: template.Statuses
            .OrderBy(s => s.SortOrder)
            .Select(s => new WorkflowStatusDto(s.Code, s.Name, s.IsTerminal, s.SortOrder))
            .ToList());
}
