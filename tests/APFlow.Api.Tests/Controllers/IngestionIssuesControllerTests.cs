using APFlow.Api.Controllers;
using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace APFlow.Api.Tests.Controllers;

/// <summary>
/// Exercises IngestionIssuesController (WP-076) - the thin HTTP wrapper over
/// IIngestionIssueQueryService, same convention as WorkflowTemplateControllerTests
/// (WP-075).
/// </summary>
public class IngestionIssuesControllerTests
{
    [Fact]
    public async Task Get_ReturnsThePageFromTheQueryService()
    {
        var page = new PagedResult<IngestionIssueDto>(
            Items: [new IngestionIssueDto(Guid.NewGuid(), "vendor@example.com", "Vendor Co", "No invoice", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, 1, "NO_PROCESSABLE_ATTACHMENTS", "photo.png (image/png)")],
            TotalCount: 1,
            Page: 1,
            PageSize: 25);
        var queryService = new FakeIngestionIssueQueryService { PageToReturn = page };
        var controller = new IngestionIssuesController(queryService);

        var actionResult = await controller.Get();

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<PagedResult<IngestionIssueDto>>(okResult.Value);
        Assert.Equal(page, response);
    }

    [Fact]
    public async Task Get_PassesPagingParametersThrough()
    {
        var queryService = new FakeIngestionIssueQueryService
        {
            PageToReturn = new PagedResult<IngestionIssueDto>([], 0, 2, 10),
        };
        var controller = new IngestionIssuesController(queryService);

        await controller.Get(page: 2, pageSize: 10, sortDescending: false);

        Assert.NotNull(queryService.LastParameters);
        Assert.Equal(2, queryService.LastParameters!.Page);
        Assert.Equal(10, queryService.LastParameters.PageSize);
        Assert.False(queryService.LastParameters.SortDescending);
    }

    [Fact]
    public async Task Get_InvalidPage_ReturnsBadRequestWithCode()
    {
        var queryService = new FakeIngestionIssueQueryService
        {
            FailureToReturn = new Error("IngestionIssueQuery.InvalidPage", "Page must be 1 or greater."),
        };
        var controller = new IngestionIssuesController(queryService);

        var actionResult = await controller.Get(page: 0);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("IngestionIssueQuery.InvalidPage", problemDetails.Extensions["code"]);
    }

    private sealed class FakeIngestionIssueQueryService : IIngestionIssueQueryService
    {
        public PagedResult<IngestionIssueDto>? PageToReturn { get; set; }
        public Error? FailureToReturn { get; set; }
        public IngestionIssueQueryParameters? LastParameters { get; private set; }

        public Task<Result<PagedResult<IngestionIssueDto>>> SearchAsync(IngestionIssueQueryParameters parameters, CancellationToken cancellationToken = default)
        {
            LastParameters = parameters;
            return Task.FromResult(FailureToReturn is { } error
                ? Result.Failure<PagedResult<IngestionIssueDto>>(error)
                : Result.Success(PageToReturn!));
        }
    }
}
