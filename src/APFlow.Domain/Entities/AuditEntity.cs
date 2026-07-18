namespace APFlow.Domain.Entities;

/// <summary>
/// Base type for entities requiring an audit trail: who created/modified/deleted the
/// row and when. Per the architecture principle "auditability built into the data
/// model, not bolted on afterward", these fields exist on the entity itself rather
/// than in a separate audit-log table bolted on later.
/// Soft delete lives here rather than in a separate interface: financial/AP records
/// should never be hard-deleted, and "who deleted it and when" is itself audit data.
/// Values are populated automatically by <c>AppDbContext.SaveChanges</c>. This is now
/// compiler-enforced, not just convention: setters are internal, and only
/// APFlow.Infrastructure has access via [InternalsVisibleTo] (see Domain's
/// AssemblyInfo.cs) - application/feature code cannot set these directly.
/// </summary>
public abstract class AuditEntity : BaseEntity
{
    /// <summary>UTC timestamp when the row was created. Set automatically on insert.</summary>
    public DateTimeOffset CreatedAtUtc { get; internal set; }

    /// <summary>Identifier of the user (or "system") who created the row.</summary>
    public string? CreatedBy { get; internal set; }

    /// <summary>UTC timestamp when the row was last modified. Set automatically on update.</summary>
    public DateTimeOffset? ModifiedAtUtc { get; internal set; }

    /// <summary>Identifier of the user (or "system") who last modified the row.</summary>
    public string? ModifiedBy { get; internal set; }

    /// <summary>
    /// Whether this row is soft-deleted. When true, the row is excluded from normal
    /// queries via a global query filter (see <c>AppDbContext.OnModelCreating</c>) but
    /// remains in the database for audit purposes. Deleting an entity through EF Core
    /// (<c>DbSet.Remove</c>) is automatically converted into setting this flag instead
    /// of an actual DELETE - see <c>AppDbContext.SaveChanges</c>.
    /// </summary>
    public bool IsDeleted { get; internal set; }

    /// <summary>UTC timestamp when the row was soft-deleted, if applicable.</summary>
    public DateTimeOffset? DeletedAtUtc { get; internal set; }

    /// <summary>Identifier of the user who soft-deleted the row, if applicable.</summary>
    public string? DeletedBy { get; internal set; }
}
