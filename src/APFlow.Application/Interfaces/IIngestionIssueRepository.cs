using APFlow.Application.DTOs;
using APFlow.Domain.Entities;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Data access abstraction for <see cref="IngestionIssue"/> (WP-076). Same design as
/// <see cref="IAuditLogRepository"/> - plain Domain types only, tenant isolation
/// enforced by the underlying EF Core query filter, not by this interface.
/// </summary>
public interface IIngestionIssueRepository
{
    /// <summary>
    /// Returns the existing (tracked) row for the given conversation, or null if this
    /// conversation has never produced an issue before. Tracked (not
    /// <c>AsNoTracking</c>) deliberately: the pipeline's dedup logic mutates
    /// <see cref="IngestionIssue.OccurrenceCount"/>/<see cref="IngestionIssue.LastSeenUtc"/>
    /// on the returned instance and persists via <see cref="SaveChangesAsync"/>.
    /// </summary>
    Task<IngestionIssue?> GetByConversationIdAsync(string conversationId, CancellationToken cancellationToken = default);

    /// <summary>Begins tracking a new ingestion issue. Does not persist until <see cref="SaveChangesAsync"/> is called.</summary>
    Task AddAsync(IngestionIssue issue, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a sorted, paged slice of ingestion issues visible to the current
    /// tenant, together with the total count of rows (not just the page) - same
    /// shape and reasoning as <see cref="IAuditLogRepository.QueryAsync"/>.
    /// </summary>
    Task<(IReadOnlyList<IngestionIssue> Items, int TotalCount)> QueryAsync(
        IngestionIssueQueryParameters parameters, CancellationToken cancellationToken = default);

    /// <summary>Persists all pending changes made via this repository.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
