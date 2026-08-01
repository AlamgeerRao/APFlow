using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;
using APFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace APFlow.Infrastructure.Tests.Persistence;

/// <summary>
/// Exercises IngestionIssueRepository against a real AppDbContext (InMemory
/// provider - same approach as AuditLogRepositoryTests), including the pipeline's
/// central dedup claim: fetching an existing row via
/// <see cref="IIngestionIssueRepository.GetByConversationIdAsync"/>, mutating it,
/// and calling <see cref="IIngestionIssueRepository.SaveChangesAsync"/> persists
/// the change to the SAME row rather than creating a new one - proven against a
/// real (tracked) DbContext, not a hand-written fake.
/// </summary>
public class IngestionIssueRepositoryTests
{
    [Fact]
    public async Task GetByConversationIdAsync_ExistingRow_ReturnsTrackedEntity_MutationPersistsOnSameRow()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new IngestionIssueRepository(context);
        var conversationId = "conversation-1";

        await repository.AddAsync(NewIssue(tenantId, conversationId));
        await repository.SaveChangesAsync();

        var fetched = await repository.GetByConversationIdAsync(conversationId);
        Assert.NotNull(fetched);
        fetched!.OccurrenceCount++;
        fetched.LastSeenUtc = fetched.LastSeenUtc.AddMinutes(5);
        await repository.SaveChangesAsync();

        var allRows = await context.IngestionIssues.ToListAsync();
        var row = Assert.Single(allRows);
        Assert.Equal(2, row.OccurrenceCount);
    }

    [Fact]
    public async Task GetByConversationIdAsync_NoExistingRow_ReturnsNull()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new IngestionIssueRepository(context);

        var fetched = await repository.GetByConversationIdAsync("no-such-conversation");

        Assert.Null(fetched);
    }

    [Fact]
    public async Task QueryAsync_RespectsTenantIsolation()
    {
        var databaseName = Guid.NewGuid().ToString();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        using (var contextA = CreateContext(tenantA, databaseName: databaseName))
        {
            contextA.IngestionIssues.Add(NewIssue(tenantA, "conversation-a"));
            await contextA.SaveChangesAsync();
        }

        using (var contextB = CreateContext(tenantB, databaseName: databaseName))
        {
            contextB.IngestionIssues.Add(NewIssue(tenantB, "conversation-b"));
            await contextB.SaveChangesAsync();
        }

        using var queryAsA = CreateContext(tenantA, databaseName: databaseName);
        var repository = new IngestionIssueRepository(queryAsA);

        var (items, totalCount) = await repository.QueryAsync(new IngestionIssueQueryParameters());

        Assert.Equal(1, totalCount);
        Assert.Single(items);
    }

    [Fact]
    public async Task QueryAsync_SortsByLastSeenUtcDescendingByDefault()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);
        var repository = new IngestionIssueRepository(context);

        var older = NewIssue(tenantId, "conversation-older");
        older.LastSeenUtc = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var newer = NewIssue(tenantId, "conversation-newer");
        newer.LastSeenUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

        context.IngestionIssues.AddRange(older, newer);
        await context.SaveChangesAsync();

        var (items, _) = await repository.QueryAsync(new IngestionIssueQueryParameters());

        Assert.Equal(newer.Id, items[0].Id);
        Assert.Equal(older.Id, items[1].Id);
    }

    private static IngestionIssue NewIssue(Guid tenantId, string conversationId) => new()
    {
        TenantId = tenantId,
        MessageId = "msg-1",
        SenderAddress = "vendor@example.com",
        SenderName = "Vendor Co",
        Subject = "No invoice here",
        ReceivedAtUtc = DateTimeOffset.UtcNow,
        ConversationId = conversationId,
        AttachmentsFound = "photo.png (image/png)",
        ReasonCode = IngestionIssueReasonCodes.NoProcessableAttachments,
        OccurrenceCount = 1,
        LastSeenUtc = DateTimeOffset.UtcNow,
    };

    private static AppDbContext CreateContext(Guid tenantId, string? databaseName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;

        var context = new AppDbContext(options, new FakeCurrentUserService(tenantId));
        context.Database.EnsureCreated();
        return context;
    }

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(Guid tenantId)
        {
            TenantId = tenantId.ToString();
        }

        public bool IsAuthenticated => true;
        public string? UserId => "test-user";
        public string? Email => null;
        public string? DisplayName => null;
        public string? TenantId { get; }
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }
}
