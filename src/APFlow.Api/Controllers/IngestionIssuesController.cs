using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace APFlow.Api.Controllers;

/// <summary>
/// Exposes unprocessable-email records (WP-076) over HTTP - the Inbox's data
/// source. Thin wrapper over <see cref="IIngestionIssueQueryService"/>, no business
/// logic of its own, same convention as every other controller in this project.
/// </summary>
[ApiController]
[Route("api/ingestion-issues")]
public sealed class IngestionIssuesController : ControllerBase
{
    private readonly IIngestionIssueQueryService _ingestionIssueQueryService;

    /// <summary>Creates a new <see cref="IngestionIssuesController"/>.</summary>
    public IngestionIssuesController(IIngestionIssueQueryService ingestionIssueQueryService)
    {
        _ingestionIssueQueryService = ingestionIssueQueryService;
    }

    /// <summary>
    /// Returns a page of ingestion issues visible to the acting tenant, most
    /// recently seen first by default.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<IngestionIssueDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] bool sortDescending = true,
        CancellationToken cancellationToken = default)
    {
        var parameters = new IngestionIssueQueryParameters(page, pageSize, sortDescending);

        var result = await _ingestionIssueQueryService.SearchAsync(parameters, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorProblem(result.Error);
        }

        return Ok(result.Value);
    }

    private ObjectResult ErrorProblem(Error error)
    {
        var problemDetails = new ProblemDetails
        {
            Title = error.Code,
            Detail = error.Message,
            Status = StatusCodes.Status400BadRequest,
        };
        problemDetails.Extensions["code"] = error.Code;

        return StatusCode(StatusCodes.Status400BadRequest, problemDetails);
    }
}
