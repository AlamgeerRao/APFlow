using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Entities;

namespace APFlow.Application.Features.Audit;

/// <summary>
/// Default implementation of <see cref="IAuditQueryService"/>. Depends only on
/// <see cref="IAuditLogRepository"/>, so this class is fully unit-testable with a
/// fake repository. Mirrors WP-011's <c>InvoiceQueryService</c> structure.
/// </summary>
public sealed class AuditQueryService : IAuditQueryService
{
    private readonly IAuditLogRepository _repository;

    /// <summary>Creates a new <see cref="AuditQueryService"/>.</summary>
    public AuditQueryService(IAuditLogRepository repository)
    {
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<Result<PagedResult<AuditLogDto>>> SearchAsync(
        AuditLogQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(parameters);
        if (validationError is not null)
        {
            return Result.Failure<PagedResult<AuditLogDto>>(validationError);
        }

        var (items, totalCount) = await _repository.QueryAsync(parameters, cancellationToken);

        var dtoItems = items.Select(ToDto).ToList();

        return Result.Success(new PagedResult<AuditLogDto>(dtoItems, totalCount, parameters.Page, parameters.PageSize));
    }

    /// <summary>Mirrors WP-011's InvoiceQueryService.Validate exactly (page/page-size bounds, date range ordering).</summary>
    private static Error? Validate(AuditLogQueryParameters parameters)
    {
        if (parameters.Page < 1)
        {
            return new Error("AuditLogQuery.InvalidPage", "Page must be 1 or greater.");
        }

        if (parameters.PageSize < 1 || parameters.PageSize > AuditLogQueryParameters.MaxPageSize)
        {
            return new Error(
                "AuditLogQuery.InvalidPageSize",
                $"PageSize must be between 1 and {AuditLogQueryParameters.MaxPageSize}.");
        }

        if (parameters.FromUtc is not null && parameters.ToUtc is not null && parameters.FromUtc > parameters.ToUtc)
        {
            return new Error("AuditLogQuery.InvalidDateRange", "FromUtc must not be later than ToUtc.");
        }

        return null;
    }

    private static AuditLogDto ToDto(AuditLog log) => new(
        Id: log.Id,
        PerformedByUserId: log.CreatedBy,
        Action: log.Action,
        EntityName: log.EntityName,
        EntityId: log.EntityId,
        PreviousValue: log.PreviousValue,
        NewValue: log.NewValue,
        PerformedAtUtc: log.CreatedAtUtc);
}
