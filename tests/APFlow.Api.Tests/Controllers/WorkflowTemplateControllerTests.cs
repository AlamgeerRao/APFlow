using APFlow.Api.Controllers;
using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Common.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace APFlow.Api.Tests.Controllers;

/// <summary>
/// Exercises WorkflowTemplateController (WP-075) - the thin HTTP wrapper this WP
/// added over the existing, already-tested IWorkflowQueryService, so the frontend
/// nav (and any other future consumer) has a real endpoint to call instead of the
/// FixtureWorkflowTemplateClient it was still stuck on.
/// </summary>
public class WorkflowTemplateControllerTests
{
    [Fact]
    public async Task GetCurrent_ReturnsTheActiveTemplate_ForTheInvoiceDomain()
    {
        var template = new WorkflowTemplateDto(
            Id: Guid.NewGuid(),
            DomainName: WorkflowDomains.Invoice,
            Name: "GB Skips",
            IsTenantSpecific: true,
            Statuses:
            [
                new WorkflowStatusDto("AWAITING_REVIEW", "Awaiting Review", false, 4),
                new WorkflowStatusDto("CHECKED_READY_TO_APPROVE", "Checked & Ready to Approve", false, 5),
                new WorkflowStatusDto("NEEDS_REVIEW_FEBINA", "Needs Review by Febina", false, 6),
                new WorkflowStatusDto("APPROVED", "Approved", false, 7),
            ],
            Transitions: []);
        var queryService = new FakeWorkflowQueryService { TemplateToReturn = template };
        var controller = new WorkflowTemplateController(queryService);

        var actionResult = await controller.GetCurrent(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<WorkflowTemplateDto>(okResult.Value);
        Assert.Equal(template, response);
        Assert.Equal(WorkflowDomains.Invoice, queryService.LastDomainName);
    }

    [Fact]
    public async Task GetCurrent_TemplateNotFound_ReturnsNotFoundWithCode()
    {
        var queryService = new FakeWorkflowQueryService
        {
            FailureToReturn = new Error("Workflow.TemplateNotFound", "No workflow template configured."),
        };
        var controller = new WorkflowTemplateController(queryService);

        var actionResult = await controller.GetCurrent(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status404NotFound, objectResult.StatusCode);
        var problemDetails = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal("Workflow.TemplateNotFound", problemDetails.Extensions["code"]);
    }

    private sealed class FakeWorkflowQueryService : IWorkflowQueryService
    {
        public WorkflowTemplateDto? TemplateToReturn { get; set; }
        public Error? FailureToReturn { get; set; }
        public string? LastDomainName { get; private set; }

        public Task<Result<WorkflowTemplateDto>> GetActiveTemplateAsync(string domainName, CancellationToken cancellationToken = default)
        {
            LastDomainName = domainName;
            return Task.FromResult(FailureToReturn is { } error
                ? Result.Failure<WorkflowTemplateDto>(error)
                : Result.Success(TemplateToReturn!));
        }
    }
}
