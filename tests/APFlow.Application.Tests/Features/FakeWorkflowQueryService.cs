using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;

namespace APFlow.Application.Tests.Features;

/// <summary>Hand-written fake, same pattern as every fake elsewhere in this codebase.</summary>
internal sealed class FakeWorkflowQueryService : IWorkflowQueryService
{
    public WorkflowTemplateDto? TemplateToReturn { get; set; }
    public Error? FailureToReturn { get; set; }

    public Task<Result<WorkflowTemplateDto>> GetActiveTemplateAsync(string domainName, CancellationToken cancellationToken = default) =>
        Task.FromResult(FailureToReturn is { } error
            ? Result.Failure<WorkflowTemplateDto>(error)
            : Result.Success(TemplateToReturn!));
}
