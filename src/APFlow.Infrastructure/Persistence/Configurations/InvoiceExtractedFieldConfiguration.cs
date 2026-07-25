using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APFlow.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="InvoiceExtractedField"/> (WP-056).</summary>
public sealed class InvoiceExtractedFieldConfiguration : IEntityTypeConfiguration<InvoiceExtractedField>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<InvoiceExtractedField> builder)
    {
        builder.ToTable("InvoiceExtractedFields");

        builder.HasKey(f => f.Id);

        builder.Property(f => f.FieldKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(f => f.Label)
            .IsRequired()
            .HasMaxLength(200);

        // Nullable - a field Document Intelligence did not extract for this
        // invoice is a normal outcome, not an error (see
        // InvoiceExtractedField.Value's own doc comment). 4000 matches
        // InvoiceNoteConfiguration's Content column - the largest free-text
        // value this codebase currently persists, and a safe upper bound for a
        // single extracted field's display text.
        builder.Property(f => f.Value)
            .HasMaxLength(4000);

        builder.HasIndex(f => new { f.TenantId, f.InvoiceId });
    }
}
