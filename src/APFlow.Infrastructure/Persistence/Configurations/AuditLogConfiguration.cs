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

        // The natural "show me this entity's history" lookup - AuditLogQueryService's
        // primary filter shape (EntityName + EntityId, optionally further narrowed).
        builder.HasIndex(a => new { a.TenantId, a.EntityName, a.EntityId });
    }
}
