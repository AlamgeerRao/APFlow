namespace APFlow.Domain.Entities;

/// <summary>
/// Base type for all persisted Domain entities. Provides identity only - audit and
/// tenant concerns are added by <see cref="AuditEntity"/> and <see cref="TenantEntity"/>
/// respectively, so an entity opts into exactly the concerns it needs.
/// </summary>
public abstract class BaseEntity
{
    /// <summary>
    /// The entity's unique identifier. Generated client-side at construction time
    /// (via <see cref="Guid.CreateVersion7()"/>, a time-ordered GUID) rather than by the
    /// database, so an entity has a stable identity from the moment it is created in
    /// code, before it is ever persisted. Time-ordered GUIDs also avoid the index
    /// fragmentation that fully random GUIDs cause as a SQL Server clustered/primary
    /// key. APFlow.Infrastructure configures EF Core with <c>ValueGeneratedNever()</c>
    /// for this property to match - see <c>AppDbContext.OnModelCreating</c>.
    /// CAVEAT: "time-ordered" refers to the RFC 9562 big-endian byte layout, not
    /// .NET's default <see cref="Guid.CompareTo(Guid)"/>/sort order, which uses a different
    /// internal field layout and does NOT reliably preserve chronological order
    /// (confirmed empirically - see BaseEntityTests). Do not sort entities by
    /// <see cref="Id"/> in LINQ/application code and expect chronological order; use
    /// <see cref="AuditEntity.CreatedAtUtc"/> (on <see cref="AuditEntity"/>) for that instead. The
    /// SQL Server index-fragmentation benefit is a separate, storage-layer concern and
    /// is unaffected by this.
    /// </summary>
    public Guid Id { get; protected init; } = Guid.CreateVersion7();
}
