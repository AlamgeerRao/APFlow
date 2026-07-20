using APFlow.Application.Features.Audit;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common.Constants;

namespace APFlow.Application.DTOs;

/// <summary>
/// Request to record a single audit trail entry via <see cref="IAuditService"/>.
/// No "performed by" field: unlike <see cref="APFlow.Domain.Entities.AuditLog.Action"/>
/// and the other fields below, "who performed this" is already handled centrally by
/// <c>AppDbContext.ApplyAuditAndSoftDeleteConventions</c> (the same mechanism every
/// other entity's <c>CreatedBy</c> uses) - a caller does not, and should not, supply
/// it directly. See docs/WP-013-Audit-Logging-Decisions.md.
/// </summary>
/// <param name="Action">What happened - see <see cref="AuditActions"/> for known values.</param>
/// <param name="EntityName">The name of the entity type this action was performed against, e.g. "Invoice".</param>
/// <param name="EntityId">The affected entity's id.</param>
/// <param name="PreviousValue">A plain-text representation of the value before this action, or null if not applicable.</param>
/// <param name="NewValue">A plain-text representation of the value after this action, or null if not applicable.</param>
public sealed record RecordAuditLogRequest(
    string Action,
    string EntityName,
    Guid EntityId,
    string? PreviousValue,
    string? NewValue);

/// <summary>
/// Read shape for an audit log entry - the query-side projection of
/// <see cref="APFlow.Domain.Entities.AuditLog"/>, including the two fields
/// (<see cref="PerformedByUserId"/>, <see cref="PerformedAtUtc"/>) that live on the
/// entity's inherited <c>AuditEntity.CreatedBy</c>/<c>CreatedAtUtc</c> rather than as
/// dedicated columns - see that entity's doc comment for why. Renamed here to their
/// WP-013 task-list names since a read consumer shouldn't need to know they are
/// reused base-class fields to understand what they mean.
/// </summary>
public sealed record AuditLogDto(
    Guid Id,
    string? PerformedByUserId,
    string Action,
    string EntityName,
    Guid EntityId,
    string? PreviousValue,
    string? NewValue,
    DateTimeOffset PerformedAtUtc);

/// <summary>
/// Filter, paging, and sort parameters for <see cref="IAuditQueryService"/> /
/// <see cref="IAuditLogRepository.QueryAsync"/>. Mirrors WP-011's
/// <see cref="InvoiceQueryParameters"/>/<see cref="PagedResult{T}"/> shape and
/// conventions deliberately, rather than inventing a new query pattern for this
/// entity. No <c>SortBy</c> field (unlike <see cref="InvoiceQueryParameters"/>): an
/// append-only audit trail has exactly one meaningful chronological ordering
/// (<c>CreatedAtUtc</c>/<see cref="AuditLogDto.PerformedAtUtc"/>), so a sort-field
/// selector would be a choice with no real alternative to select between.
/// </summary>
/// <param name="EntityName">Restrict to entries for this entity type (e.g. "Invoice"), if set.</param>
/// <param name="EntityId">Restrict to entries for this specific entity instance, if set. Combine with <paramref name="EntityName"/> for "show me this invoice's history".</param>
/// <param name="PerformedByUserId">Restrict to entries performed by this user id (or "system"), if set.</param>
/// <param name="FromUtc">Restrict to entries recorded on or after this UTC instant, if set.</param>
/// <param name="ToUtc">Restrict to entries recorded on or before this UTC instant, if set.</param>
/// <param name="Page">1-based page number. Must be 1 or greater.</param>
/// <param name="PageSize">Rows per page. Must be between 1 and <see cref="MaxPageSize"/>.</param>
/// <param name="SortDescending">Sort direction by recorded time. Defaults to descending (most recent first).</param>
public sealed record AuditLogQueryParameters(
    string? EntityName = null,
    Guid? EntityId = null,
    string? PerformedByUserId = null,
    DateTimeOffset? FromUtc = null,
    DateTimeOffset? ToUtc = null,
    int Page = 1,
    int PageSize = 25,
    bool SortDescending = true)
{
    /// <summary>
    /// Upper bound on <see cref="PageSize"/>. Enforced (returning a validation
    /// <c>Error</c>) by <see cref="AuditQueryService"/>, and enforced
    /// again defensively (by clamping, not throwing) inside
    /// <c>APFlow.Infrastructure.Persistence.AuditLogRepository.QueryAsync</c> - same
    /// defense-in-depth pattern as WP-011's <see cref="InvoiceQueryParameters.MaxPageSize"/>.
    /// </summary>
    public const int MaxPageSize = 100;
}
