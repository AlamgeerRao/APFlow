using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APFlow.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="InvoiceNote"/>.</summary>
public sealed class InvoiceNoteConfiguration : IEntityTypeConfiguration<InvoiceNote>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<InvoiceNote> builder)
    {
        builder.ToTable("InvoiceNotes");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Content)
            .IsRequired()
            .HasMaxLength(4000);

        builder.HasIndex(n => new { n.TenantId, n.InvoiceId });
    }
}
