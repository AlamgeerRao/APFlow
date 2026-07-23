using APFlow.Application.Interfaces;
using APFlow.Domain.Common;

namespace APFlow.Application.Features.Workflow;

/// <summary>
/// Default implementation of <see cref="IWorkflowValidationService"/>. Depends only
/// on <see cref="IWorkflowTemplateRepository"/>, so this class is fully
/// unit-testable with a fake repository. See the interface's doc comment for why
/// this is not yet called from any blocking enforcement path.
/// </summary>
public sealed class WorkflowValidationService : IWorkflowValidationService
{
    private readonly IWorkflowTemplateRepository _repository;

    /// <summary>Creates a new <see cref="WorkflowValidationService"/>.</summary>
    public WorkflowValidationService(IWorkflowTemplateRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<Result> ValidateTransitionAsync(
        string domainName, string fromStatusCode, string toStatusCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(domainName))
        {
            return Result.Failure(new Error("Workflow.InvalidDomainName", "DomainName must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(fromStatusCode) || string.IsNullOrWhiteSpace(toStatusCode))
        {
            return Result.Failure(new Error("Workflow.InvalidStatusCode", "FromStatusCode and ToStatusCode must not be empty."));
        }

        // A no-op "transition" (unchanged status) is always allowed - nothing is
        // actually moving, so there is nothing for a WorkflowTransition row to
        // permit or forbid. Callers (e.g. InvoiceService.UpdateAsync) are expected
        // to skip calling this at all when the status hasn't changed, but this is
        // deliberately also safe to call in that case.
        if (string.Equals(fromStatusCode, toStatusCode, StringComparison.Ordinal))
        {
            return Result.Success();
        }

        var template = await _repository.GetActiveTemplateAsync(domainName, cancellationToken);
        if (template is null)
        {
            return Result.Failure(new Error(
                "Workflow.TemplateNotFound",
                $"No workflow template (platform-default or tenant-specific) is configured for domain '{domainName}'."));
        }

        var validCodes = template.Statuses.Select(s => s.Code).ToHashSet(StringComparer.Ordinal);

        if (!validCodes.Contains(fromStatusCode))
        {
            return Result.Failure(new Error(
                "Workflow.InvalidFromStatus", $"'{fromStatusCode}' is not a valid status for template '{template.Name}'."));
        }

        if (!validCodes.Contains(toStatusCode))
        {
            return Result.Failure(new Error(
                "Workflow.InvalidToStatus", $"'{toStatusCode}' is not a valid status for template '{template.Name}'."));
        }

        var isAllowed = template.Transitions.Any(t =>
            string.Equals(t.FromStatusCode, fromStatusCode, StringComparison.Ordinal)
            && string.Equals(t.ToStatusCode, toStatusCode, StringComparison.Ordinal));

        return isAllowed
            ? Result.Success()
            : Result.Failure(new Error(
                "Workflow.TransitionNotAllowed",
                $"'{fromStatusCode}' -> '{toStatusCode}' is not an allowed transition for template '{template.Name}'."));
    }
}
