using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Common.Constants;
using Microsoft.AspNetCore.Mvc;

namespace APFlow.Api.Controllers;

/// <summary>
/// Exposes the acting tenant's active Invoice WorkflowTemplate (WP-050) over HTTP
/// (WP-075). <see cref="IWorkflowQueryService"/> has existed and been fully tested
/// since WP-050 - already used internally by <c>SupplierFolderQueryService</c>
/// (WP-059) and <c>InvoiceWorkflowActionsService</c> (WP-054) - but nothing ever
/// exposed it to callers outside the API process itself. That gap, not a caching
/// bug or wrong tenant resolution, was WP-075's actual root cause: the frontend's
/// nav was still reading <c>FixtureWorkflowTemplateClient</c> because there was no
/// real endpoint to swap to yet. This controller adds no business logic of its
/// own - a thin wrapper over the existing service, same convention as
/// <c>InvoicesController</c>'s own doc comment.
/// </summary>
[ApiController]
[Route("api/workflow-template")]
public sealed class WorkflowTemplateController : ControllerBase
{
    private readonly IWorkflowQueryService _workflowQueryService;

    /// <summary>Creates a new <see cref="WorkflowTemplateController"/>.</summary>
    public WorkflowTemplateController(IWorkflowQueryService workflowQueryService)
    {
        _workflowQueryService = workflowQueryService;
    }

    /// <summary>
    /// Returns the acting tenant's active Invoice WorkflowTemplate - the
    /// tenant-specific one if configured (e.g. GB Skips' 15-status template),
    /// otherwise the platform default (WP-050's own resolution rule, unchanged
    /// here). Includes every status (terminal and non-terminal) with its
    /// <see cref="WorkflowStatusDto.IsTerminal"/>/<see cref="WorkflowStatusDto.SortOrder"/>
    /// flags - a status-badge-style consumer needs terminal statuses too (an
    /// invoice already at PAID/CANCELLED/ARCHIVED still needs a
    /// display name), unlike <c>GET /api/invoices/folders</c> (WP-059), which
    /// deliberately only returns non-terminal statuses for its own folder-list
    /// purpose and was considered but rejected as a reuse target for that reason.
    /// Only the Invoice domain exists today (<see cref="WorkflowDomains.Invoice"/>)
    /// - no domain route/query parameter is exposed, since inventing one for a
    /// domain that doesn't exist yet would be speculative, not a real requirement.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(WorkflowTemplateDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrent(CancellationToken cancellationToken)
    {
        var result = await _workflowQueryService.GetActiveTemplateAsync(WorkflowDomains.Invoice, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorProblem(result.Error);
        }

        return Ok(result.Value);
    }

    private ObjectResult ErrorProblem(Error error)
    {
        var statusCode = error.Code switch
        {
            "Workflow.TemplateNotFound" => StatusCodes.Status404NotFound,
            _ => StatusCodes.Status400BadRequest,
        };

        var problemDetails = new ProblemDetails
        {
            Title = error.Code,
            Detail = error.Message,
            Status = statusCode,
        };
        problemDetails.Extensions["code"] = error.Code;

        return StatusCode(statusCode, problemDetails);
    }
}
