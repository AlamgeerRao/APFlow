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

        // Nullable (a token may lack a name claim - see InvoiceNote.AuthorDisplayName's
        // own doc comment); 200 matches this codebase's existing convention for a
        // display-style name column (see StatusReferenceConfiguration/
        // WorkflowTemplateConfiguration's own .Name columns).
        builder.Property(n => n.AuthorDisplayName)
            .HasMaxLength(200);

        builder.HasIndex(n => new { n.TenantId, n.InvoiceId });
    }
}
