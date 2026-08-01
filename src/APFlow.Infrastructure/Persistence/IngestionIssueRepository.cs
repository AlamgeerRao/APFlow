using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace APFlow.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IIngestionIssueRepository"/>. Tenant
/// isolation on every read comes from AppDbContext's query filter, not from any
/// logic here - this class does not reference tenant/current-user state at all.
/// </summary>
public sealed class IngestionIssueRepository : IIngestionIssueRepository
{
    private readonly AppDbContext _context;

    /// <summary>Creates the repository over the given <see cref="AppDbContext"/>.</summary>
    public IngestionIssueRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public Task<IngestionIssue?> GetByConversationIdAsync(string conversationId, CancellationToken cancellationToken = default) =>
        _context.IngestionIssues.FirstOrDefaultAsync(i => i.ConversationId == conversationId, cancellationToken);

    /// <inheritdoc/>
    public async Task AddAsync(IngestionIssue issue, CancellationToken cancellationToken = default) =>
        await _context.IngestionIssues.AddAsync(issue, cancellationToken);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<IngestionIssue> Items, int TotalCount)> QueryAsync(
        IngestionIssueQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = _context.IngestionIssues.AsNoTracking().AsQueryable();

        var totalCount = await query.CountAsync(cancellationToken);

        query = parameters.SortDescending
            ? query.OrderByDescending(i => i.LastSeenUtc)
            : query.OrderBy(i => i.LastSeenUtc);

        // Defensive clamp, not the primary validation - see
        // IngestionIssueQueryParameters.MaxPageSize's doc comment, same pattern as
        // WP-013's AuditLogRepository.QueryAsync.
        var page = Math.Max(parameters.Page, 1);
        var pageSize = Math.Clamp(parameters.PageSize, 1, IngestionIssueQueryParameters.MaxPageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <inheritdoc/>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
