using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APFlow.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for <see cref="Invoice"/>. Money fields use HasPrecision(18, 2) -
/// SQL Server's decimal columns need an explicit precision/scale, and EF Core will
/// otherwise fall back to a default that isn't guaranteed and produces a build-time
/// warning. (18, 2) is a conventional, generous choice for currency amounts; revisit
/// if a currency requiring more decimal places is ever supported.
/// </summary>
public sealed class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("Invoices");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.SupplierInvoiceNumber)
            .HasMaxLength(128);

        builder.Property(i => i.Currency)
            .HasMaxLength(3); // ISO 4217 currency codes are always 3 characters.

        builder.Property(i => i.NetAmount).HasPrecision(18, 2);
        builder.Property(i => i.Vat).HasPrecision(18, 2);
        builder.Property(i => i.GrossTotal).HasPrecision(18, 2);

        // WP-050: Status is a plain string (StatusReference.Code), not an enum -
        // no HasConversion needed anymore. Length matches StatusReferenceConfiguration's
        // own Code column, since a value here must always match one of those rows.
        builder.Property(i => i.Status)
            .IsRequired()
            .HasMaxLength(64);

        builder.Property(i => i.SourceEmailMessageId)
            .HasMaxLength(512); // Graph message ids are long opaque strings.

        // Matches BlobStorageService.MaxPhysicalBlobNameLength - Azure's own blob
        // name length ceiling - since this stores a full logical blob name.
        builder.Property(i => i.SourceDocumentBlobName)
            .HasMaxLength(1024);

        // SHA-256 as lowercase hex is always exactly 64 characters.
        builder.Property(i => i.SourceDocumentContentHash)
            .HasMaxLength(64);

        // Supports WP-052's content-hash-based idempotency lookup without a full
        // table scan per tenant - the same reasoning as the existing
        // {TenantId, InvoiceDate} index below, for the pipeline's new dedup key.
        builder.HasIndex(i => new { i.TenantId, i.SourceDocumentContentHash });

        builder.Property(i => i.IsPotentialDuplicate)
            .HasDefaultValue(false);

        // Free-text summary of DuplicateMatch.Reason(s) - same length ceiling as
        // InvoiceNoteConfiguration's Content column (FieldLimits.InvoiceNoteContent),
        // since it's the same kind of free-text explanatory field.
        builder.Property(i => i.DuplicateCheckReason)
            .HasMaxLength(4000);

        builder.HasIndex(i => new { i.TenantId, i.Status });
        builder.HasIndex(i => new { i.TenantId, i.SupplierId });

        // Supports WP-011's InvoiceDateFrom/InvoiceDateTo range filter
        // (InvoiceRepository.QueryAsync) without a full table scan per tenant.
        // No index added for SupplierInvoiceNumber: WP-011's filter on it is a
        // substring match (Contains), which a standard B-tree index cannot serve
        // efficiently regardless - only a full-text/trigram index would help, and
        // nothing in WP-011's scope calls for that infrastructure.
        builder.HasIndex(i => new { i.TenantId, i.InvoiceDate });

        builder.HasMany(i => i.Notes)
            .WithOne(n => n.Invoice)
            .HasForeignKey(n => n.InvoiceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
