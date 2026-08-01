using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APFlow.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="IngestionIssue"/>.</summary>
public sealed class IngestionIssueConfiguration : IEntityTypeConfiguration<IngestionIssue>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<IngestionIssue> builder)
    {
        builder.ToTable("IngestionIssues");

        builder.HasKey(i => i.Id);

        builder.Property(i => i.MessageId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.SenderAddress)
            .IsRequired()
            .HasMaxLength(320);

        builder.Property(i => i.SenderName)
            .HasMaxLength(200);

        builder.Property(i => i.Subject)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(i => i.ConversationId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(i => i.AttachmentsFound)
            .IsRequired()
            .HasMaxLength(2000);

        builder.Property(i => i.ReasonCode)
            .IsRequired()
            .HasMaxLength(50);

        // The pipeline's dedup lookup - find the existing row for a conversation
        // within the acting tenant, same shape as AuditLogConfiguration's
        // TenantId-first composite index for its own primary lookup.
        builder.HasIndex(i => new { i.TenantId, i.ConversationId });
    }
}
