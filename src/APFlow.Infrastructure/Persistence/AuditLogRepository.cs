using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace APFlow.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of <see cref="IAuditLogRepository"/>. Tenant isolation on
/// every read comes from AppDbContext's query filter, not from any logic here - this
/// class does not reference tenant/current-user state at all.
/// </summary>
public sealed class AuditLogRepository : IAuditLogRepository
{
    private readonly AppDbContext _context;

    /// <summary>Creates the repository over the given <see cref="AppDbContext"/>.</summary>
    public AuditLogRepository(AppDbContext context)
    {
        _context = context;
    }

    /// <inheritdoc/>
    public Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.AuditLogs.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    /// <inheritdoc/>
    public async Task<(IReadOnlyList<AuditLog> Items, int TotalCount)> QueryAsync(
        AuditLogQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(parameters.EntityName))
        {
            query = query.Where(a => a.EntityName == parameters.EntityName);
        }

        if (parameters.EntityId is not null)
        {
            query = query.Where(a => a.EntityId == parameters.EntityId);
        }

        if (!string.IsNullOrWhiteSpace(parameters.PerformedByUserId))
        {
            query = query.Where(a => a.CreatedBy == parameters.PerformedByUserId);
        }

        if (parameters.FromUtc is not null)
        {
            query = query.Where(a => a.CreatedAtUtc >= parameters.FromUtc);
        }

        if (parameters.ToUtc is not null)
        {
            query = query.Where(a => a.CreatedAtUtc <= parameters.ToUtc);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        query = parameters.SortDescending
            ? query.OrderByDescending(a => a.CreatedAtUtc)
            : query.OrderBy(a => a.CreatedAtUtc);

        // Defensive clamp, not the primary validation - see AuditLogQueryParameters.MaxPageSize's
        // doc comment, same pattern as WP-011's InvoiceRepository.QueryAsync.
        var page = Math.Max(parameters.Page, 1);
        var pageSize = Math.Clamp(parameters.PageSize, 1, AuditLogQueryParameters.MaxPageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    /// <inheritdoc/>
    public async Task AddAsync(AuditLog auditLog, CancellationToken cancellationToken = default) =>
        await _context.AuditLogs.AddAsync(auditLog, cancellationToken);

    /// <inheritdoc/>
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
        _context.SaveChangesAsync(cancellationToken);
}
