using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Common.Constants;

namespace APFlow.Application.Features.Invoices;

/// <summary>
/// Default implementation of <see cref="IInvoiceWorkflowActionsService"/>.
/// Depends only on <see cref="IInvoiceRepository"/>, <see cref="IWorkflowQueryService"/>,
/// <see cref="IApprovalAuthorizationService"/>, and <see cref="ICurrentUserService"/> -
/// all plain, EF-Core-free interfaces - so this class is fully unit-testable with
/// fake repositories/services, same as <see cref="InvoiceService"/>.
/// </summary>
public sealed class InvoiceWorkflowActionsService : IInvoiceWorkflowActionsService
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly IWorkflowQueryService _workflowQueryService;
    private readonly IApprovalAuthorizationService _approvalAuthorizationService;
    private readonly ICurrentUserService _currentUserService;

    /// <summary>Creates a new <see cref="InvoiceWorkflowActionsService"/>.</summary>
    public InvoiceWorkflowActionsService(
        IInvoiceRepository invoiceRepository,
        IWorkflowQueryService workflowQueryService,
        IApprovalAuthorizationService approvalAuthorizationService,
        ICurrentUserService currentUserService)
    {
        _invoiceRepository = invoiceRepository;
        _workflowQueryService = workflowQueryService;
        _approvalAuthorizationService = approvalAuthorizationService;
        _currentUserService = currentUserService;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<AvailableActionDto>>> GetAvailableActionsAsync(
        Guid invoiceId, CancellationToken cancellationToken = default)
    {
        // Same "Invoice.NotFound" code/message shape as InvoiceService, so a
        // caller sees an identical failure regardless of which endpoint they hit.
        var invoice = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure<IReadOnlyList<AvailableActionDto>>(
                new Error("Invoice.NotFound", $"Invoice '{invoiceId}' was not found."));
        }

        var templateResult = await _workflowQueryService.GetActiveTemplateAsync(WorkflowDomains.Invoice, cancellationToken);
        if (templateResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<AvailableActionDto>>(templateResult.Error);
        }

        var template = templateResult.Value;
        var statusLabelsByCode = template.Statuses.ToDictionary(s => s.Code, s => s.Name, StringComparer.Ordinal);

        var actions = new List<AvailableActionDto>();

        foreach (var transition in template.Transitions.Where(
            t => string.Equals(t.FromStatusCode, invoice.Status, StringComparison.Ordinal)))
        {
            if (RoleGatedTransitions.RequiresApprovalRole(invoice.Status, transition.ToStatusCode))
            {
                var authorizationResult = await _approvalAuthorizationService.AuthorizeAsync(
                    ApprovalDomains.InvoiceApproval, _currentUserService.Roles, cancellationToken);

                if (authorizationResult.IsFailure)
                {
                    // Not permitted for THIS caller right now - omitted entirely,
                    // not reported as an error. This is what task 1 means by "not
                    // the full graph with permission flags".
                    continue;
                }
            }

            var label = statusLabelsByCode.TryGetValue(transition.ToStatusCode, out var name)
                ? name
                : transition.ToStatusCode;

            actions.Add(new AvailableActionDto(transition.ToStatusCode, label));
        }

        return Result.Success<IReadOnlyList<AvailableActionDto>>(actions);
    }
}
