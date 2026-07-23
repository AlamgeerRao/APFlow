using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APFlow.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping and seed data for <see cref="StatusReference"/> (WP-050 tasks 1/2/3).</summary>
public sealed class StatusReferenceConfiguration : IEntityTypeConfiguration<StatusReference>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<StatusReference> builder)
    {
        builder.ToTable("StatusReferences");
        builder.HasKey(s => s.Id);

        builder.Property(s => s.Code).IsRequired().HasMaxLength(64);
        builder.Property(s => s.Name).IsRequired().HasMaxLength(200);

        builder.HasIndex(s => new { s.WorkflowTemplateId, s.Code }).IsUnique();

        builder.HasData(WorkflowSeedData.PlatformDefaultStatuses.Concat(WorkflowSeedData.GbSkipsStatuses).Select(row =>
            new
            {
                Id = row.Id,
                WorkflowTemplateId = row.TemplateId,
                TenantId = row.TemplateId == WorkflowSeedData.GbSkipsTemplateId
                    ? (Guid?)WorkflowSeedData.GbSkipsPlaceholderTenantId
                    : null,
                Code = row.Code,
                Name = row.Name,
                IsTerminal = row.IsTerminal,
                SortOrder = row.SortOrder,
                CreatedAtUtc = WorkflowSeedData.SeedTimestamp,
                CreatedBy = "system",
                IsDeleted = false,
            }));
    }
}
