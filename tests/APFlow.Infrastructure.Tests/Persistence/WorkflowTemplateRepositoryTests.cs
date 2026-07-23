using APFlow.Application.Interfaces;
using APFlow.Domain.Common.Constants;
using APFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace APFlow.Infrastructure.Tests.Persistence;

/// <summary>
/// Exercises WorkflowTemplateRepository against a real AppDbContext (InMemory
/// provider) - critically, using the REAL seeded data (WorkflowSeedData), not
/// test-constructed fake templates, proving the actual HasData seed rows resolve
/// correctly per tenant. EF Core applies HasData seed data at the model level
/// regardless of provider, so this runs without needing to apply the real
/// migration.
/// </summary>
public class WorkflowTemplateRepositoryTests
{
    [Fact]
    public async Task GetActiveTemplateAsync_PlatformDefaultTenant_ReturnsBaselineThirteenStatuses_NoGbSkipsExtras()
    {
        using var context = CreateContext(tenantId: Guid.NewGuid()); // a tenant with no template of its own
        var repository = new WorkflowTemplateRepository(context);

        var template = await repository.GetActiveTemplateAsync(WorkflowDomains.Invoice);

        Assert.NotNull(template);
        Assert.Null(template!.TenantId);
        // 06_Domain_Reference_Data.md §2's 13 statuses, plus EXTRACTED - see
        // InvoiceStatusCodes.Extracted's doc comment for why that one extra status
        // is included despite not being in that document.
        Assert.Equal(14, template.Statuses.Count);
        Assert.Contains(template.Statuses, s => s.Code == InvoiceStatusCodes.Extracted);
        Assert.DoesNotContain(template.Statuses, s => s.Code == InvoiceStatusCodes.CheckedReadyToApprove);
        Assert.DoesNotContain(template.Statuses, s => s.Code == InvoiceStatusCodes.NeedsReviewFebina);
        Assert.Empty(template.Transitions); // WP-050: no transitions seeded yet
    }

    [Fact]
    public async Task GetActiveTemplateAsync_GbSkipsTenant_ReturnsGbSkipsTemplate_WithTheTwoExtraStatuses()
    {
        using var context = CreateContext(WorkflowSeedData.GbSkipsPlaceholderTenantId);
        var repository = new WorkflowTemplateRepository(context);

        var template = await repository.GetActiveTemplateAsync(WorkflowDomains.Invoice);

        Assert.NotNull(template);
        Assert.Equal(WorkflowSeedData.GbSkipsPlaceholderTenantId, template!.TenantId);
        Assert.Equal("GB Skips Invoice Workflow", template.Name);
        Assert.Contains(template.Statuses, s => s.Code == InvoiceStatusCodes.CheckedReadyToApprove);
        Assert.Contains(template.Statuses, s => s.Code == InvoiceStatusCodes.NeedsReviewFebina);

        // Task 3: positioned between AWAITING_REVIEW and APPROVED.
        var awaitingReview = template.Statuses.Single(s => s.Code == InvoiceStatusCodes.AwaitingReview);
        var checkedReadyToApprove = template.Statuses.Single(s => s.Code == InvoiceStatusCodes.CheckedReadyToApprove);
        var needsReviewFebina = template.Statuses.Single(s => s.Code == InvoiceStatusCodes.NeedsReviewFebina);
        var approved = template.Statuses.Single(s => s.Code == InvoiceStatusCodes.Approved);
        Assert.True(awaitingReview.SortOrder < checkedReadyToApprove.SortOrder);
        Assert.True(checkedReadyToApprove.SortOrder < approved.SortOrder);
        Assert.True(awaitingReview.SortOrder < needsReviewFebina.SortOrder);
        Assert.True(needsReviewFebina.SortOrder < approved.SortOrder);

        // WP-051 seeded exactly one transition for GB Skips (CHECKED_READY_TO_APPROVE
        // -> APPROVED, per its own explicit task 4 direction) - everything else
        // proposed in WP-050 remains unconfirmed and unseeded.
        var transition = Assert.Single(template.Transitions);
        Assert.Equal(InvoiceStatusCodes.CheckedReadyToApprove, transition.FromStatusCode);
        Assert.Equal(InvoiceStatusCodes.Approved, transition.ToStatusCode);
    }

    [Fact]
    public async Task GetActiveTemplateAsync_NoResolvableTenant_StillReturnsPlatformDefault()
    {
        // A background/system caller with no authenticated tenant should still see
        // the platform-wide default (it applies to everyone), unlike every other
        // TenantEntity-derived type in this codebase, which fails closed to zero
        // rows in this situation - see IOptionallyTenantScoped's doc comment.
        using var context = CreateContext(tenantId: null);
        var repository = new WorkflowTemplateRepository(context);

        var template = await repository.GetActiveTemplateAsync(WorkflowDomains.Invoice);

        Assert.NotNull(template);
        Assert.Null(template!.TenantId);
    }

    private static AppDbContext CreateContext(Guid? tenantId)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options, new FakeCurrentUserService(tenantId));

        // The InMemory provider only materializes HasData seed rows when the
        // database is explicitly created - unlike a real SqlServer database via
        // migrations, it does not seed automatically on first query.
        context.Database.EnsureCreated();

        return context;
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(Guid? tenantId)
        {
            TenantId = tenantId?.ToString();
        }

        public bool IsAuthenticated => TenantId is not null;
        public string? UserId => "test-user";
        public string? Email => null;
        public string? TenantId { get; }
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }
}
