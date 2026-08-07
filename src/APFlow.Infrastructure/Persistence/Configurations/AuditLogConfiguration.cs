using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APFlow.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="AuditLog"/>.</summary>
public sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(a => a.Id);

        builder.Property(a => a.Action)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.EntityName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(a => a.PreviousValue)
            .HasMaxLength(2000);

        builder.Property(a => a.NewValue)
            .HasMaxLength(2000);

        // Nullable (a token may lack a name claim, and historical rows recorded
        // before this column existed have none - see
        // AuditLog.PerformedByDisplayName's own doc comment); 200 matches this
        // codebase's existing convention for a display-style name column (same
        // as InvoiceNoteConfiguration's AuthorDisplayName).
        builder.Property(a => a.PerformedByDisplayName)
            .HasMaxLength(200);

        // The natural "show me this entity's history" lookup - AuditLogQueryService's
        // primary filter shape (EntityName + EntityId, optionally further narrowed).
        builder.HasIndex(a => new { a.TenantId, a.EntityName, a.EntityId });
    }
}
