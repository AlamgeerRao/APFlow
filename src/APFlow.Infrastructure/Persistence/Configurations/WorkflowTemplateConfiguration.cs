using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace APFlow.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping and seed data for <see cref="WorkflowTemplate"/> (WP-050 tasks 1/2/3).</summary>
public sealed class WorkflowTemplateConfiguration : IEntityTypeConfiguration<WorkflowTemplate>
{
    /// <inheritdoc/>
    public void Configure(EntityTypeBuilder<WorkflowTemplate> builder)
    {
        builder.ToTable("WorkflowTemplates");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.DomainName).IsRequired().HasMaxLength(100);
        builder.Property(t => t.Name).IsRequired().HasMaxLength(200);

        // TenantId is nullable here (platform-default templates have none) -
        // deliberately NOT the same shape as TenantEntity's mandatory TenantId.
        // See IOptionallyTenantScoped.
        builder.HasIndex(t => new { t.DomainName, t.TenantId }).IsUnique();

        builder.HasMany(t => t.Statuses)
            .WithOne(s => s.WorkflowTemplate)
            .HasForeignKey(s => s.WorkflowTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(t => t.Transitions)
            .WithOne(tr => tr.WorkflowTemplate)
            .HasForeignKey(tr => tr.WorkflowTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasData(
            new
            {
                Id = WorkflowSeedData.PlatformDefaultTemplateId,
                TenantId = (Guid?)null,
                DomainName = WorkflowDomains.Invoice,
                Name = "Platform Default",
                CreatedAtUtc = WorkflowSeedData.SeedTimestamp,
                CreatedBy = "system",
                IsDeleted = false,
            },
            new
            {
                Id = WorkflowSeedData.GbSkipsTemplateId,
                TenantId = (Guid?)WorkflowSeedData.GbSkipsPlaceholderTenantId,
                DomainName = WorkflowDomains.Invoice,
                Name = "GB Skips Invoice Workflow",
                CreatedAtUtc = WorkflowSeedData.SeedTimestamp,
                CreatedBy = "system",
                IsDeleted = false,
            });
    }
}
