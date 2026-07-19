using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APFlow.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="Supplier"/>.</summary>
public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("Suppliers");

        builder.HasKey(s => s.Id);

        builder.Property(s => s.Name)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(s => new { s.TenantId, s.Name });

        builder.HasMany(s => s.Invoices)
            .WithOne(i => i.Supplier)
            .HasForeignKey(i => i.SupplierId)
            // Restrict, not Cascade: never let deleting a Supplier row silently
            // cascade-delete financial records. Soft delete (IsDeleted) is how
            // "removal" is meant to work for audited entities in this codebase -
            // see AuditEntity - a hard delete reaching this far is already unusual.
            .OnDelete(DeleteBehavior.Restrict);
    }
}
