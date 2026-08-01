using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Entities;

namespace APFlow.Application.Features.Ingestion;

/// <summary>
/// Default implementation of <see cref="IIngestionIssueQueryService"/>. Depends only
/// on <see cref="IIngestionIssueRepository"/>, so this class is fully unit-testable
/// with a fake repository. Mirrors WP-013's <c>AuditQueryService</c> structure.
/// </summary>
public sealed class IngestionIssueQueryService : IIngestionIssueQueryService
{
    private readonly IIngestionIssueRepository _repository;

    /// <summary>Creates a new <see cref="IngestionIssueQueryService"/>.</summary>
    public IngestionIssueQueryService(IIngestionIssueRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<IngestionIssueDto>>> SearchAsync(
        IngestionIssueQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(parameters);
        if (validationError is not null)
        {
            return Result.Failure<PagedResult<IngestionIssueDto>>(validationError);
        }

        var (items, totalCount) = await _repository.QueryAsync(parameters, cancellationToken);

        var dtoItems = items.Select(ToDto).ToList();

        return Result.Success(new PagedResult<IngestionIssueDto>(dtoItems, totalCount, parameters.Page, parameters.PageSize));
    }

    /// <summary>Mirrors WP-013's AuditQueryService.Validate (page/page-size bounds only - no date range here).</summary>
    private static Error? Validate(IngestionIssueQueryParameters parameters)
    {
        if (parameters.Page < 1)
        {
            return new Error("IngestionIssueQuery.InvalidPage", "Page must be 1 or greater.");
        }

        if (parameters.PageSize < 1 || parameters.PageSize > IngestionIssueQueryParameters.MaxPageSize)
        {
            return new Error(
                "IngestionIssueQuery.InvalidPageSize",
                $"PageSize must be between 1 and {IngestionIssueQueryParameters.MaxPageSize}.");
        }

        return null;
    }

    private static IngestionIssueDto ToDto(IngestionIssue issue) => new(
        Id: issue.Id,
        SenderAddress: issue.SenderAddress,
        SenderName: issue.SenderName,
        Subject: issue.Subject,
        FirstSeenUtc: issue.ReceivedAtUtc,
        LastSeenUtc: issue.LastSeenUtc,
        OccurrenceCount: issue.OccurrenceCount,
        ReasonCode: issue.ReasonCode,
        AttachmentsFound: issue.AttachmentsFound);
}
