using APFlow.Application.DTOs;
using APFlow.Application.Features.Ingestion;
using APFlow.Application.Tests.Features;
using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;
using Xunit;

namespace APFlow.Application.Tests.Features.Ingestion;

public class IngestionIssueQueryServiceTests
{
    [Fact]
    public async Task SearchAsync_NoFilters_ReturnsAllEntries()
    {
        var (service, repository) = CreateService();
        repository.Issues.Add(NewIssue());
        repository.Issues.Add(NewIssue());

        var result = await service.SearchAsync(new IngestionIssueQueryParameters());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_AppliesPaging()
    {
        var (service, repository) = CreateService();
        for (var i = 0; i < 5; i++)
        {
            repository.Issues.Add(NewIssue());
        }

        var page1 = await service.SearchAsync(new IngestionIssueQueryParameters(Page: 1, PageSize: 2));
        var page2 = await service.SearchAsync(new IngestionIssueQueryParameters(Page: 2, PageSize: 2));

        Assert.True(page1.IsSuccess);
        Assert.Equal(5, page1.Value.TotalCount);
        Assert.Equal(3, page1.Value.TotalPages);
        Assert.Equal(2, page1.Value.Items.Count);
        Assert.Equal(2, page2.Value.Items.Count);
    }

    [Fact]
    public async Task SearchAsync_SortsByLastSeenUtcDescendingByDefault()
    {
        var (service, repository) = CreateService();
        var older = NewIssue(lastSeenUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var newer = NewIssue(lastSeenUtc: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        repository.Issues.Add(older);
        repository.Issues.Add(newer);

        var result = await service.SearchAsync(new IngestionIssueQueryParameters());

        Assert.True(result.IsSuccess);
        Assert.Equal(newer.Id, result.Value.Items[0].Id);
        Assert.Equal(older.Id, result.Value.Items[1].Id);
    }

    [Fact]
    public async Task SearchAsync_MapsFieldsIncludingOccurrenceCount()
    {
        var (service, repository) = CreateService();
        var issue = NewIssue(occurrenceCount: 3);
        repository.Issues.Add(issue);

        var result = await service.SearchAsync(new IngestionIssueQueryParameters());

        var dto = Assert.Single(result.Value.Items);
        Assert.Equal(issue.SenderAddress, dto.SenderAddress);
        Assert.Equal(issue.Subject, dto.Subject);
        Assert.Equal(issue.ReceivedAtUtc, dto.FirstSeenUtc);
        Assert.Equal(issue.LastSeenUtc, dto.LastSeenUtc);
        Assert.Equal(3, dto.OccurrenceCount);
        Assert.Equal(IngestionIssueReasonCodes.NoProcessableAttachments, dto.ReasonCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SearchAsync_InvalidPage_ReturnsFailure(int page)
    {
        var (service, _) = CreateService();

        var result = await service.SearchAsync(new IngestionIssueQueryParameters(Page: page));

        Assert.True(result.IsFailure);
        Assert.Equal("IngestionIssueQuery.InvalidPage", result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task SearchAsync_InvalidPageSize_ReturnsFailure(int pageSize)
    {
        var (service, _) = CreateService();

        var result = await service.SearchAsync(new IngestionIssueQueryParameters(PageSize: pageSize));

        Assert.True(result.IsFailure);
        Assert.Equal("IngestionIssueQuery.InvalidPageSize", result.Error.Code);
    }

    private static IngestionIssue NewIssue(int occurrenceCount = 1, DateTimeOffset? lastSeenUtc = null)
    {
        var receivedAt = DateTimeOffset.UtcNow;
        return new IngestionIssue
        {
            MessageId = "msg-1",
            SenderAddress = "vendor@example.com",
            SenderName = "Vendor Co",
            Subject = "No invoice here",
            ReceivedAtUtc = receivedAt,
            ConversationId = Guid.NewGuid().ToString(),
            AttachmentsFound = "photo.png (image/png)",
            ReasonCode = IngestionIssueReasonCodes.NoProcessableAttachments,
            OccurrenceCount = occurrenceCount,
            LastSeenUtc = lastSeenUtc ?? receivedAt,
        };
    }

    private static (IngestionIssueQueryService Service, FakeIngestionIssueRepository Repository) CreateService()
    {
        var repository = new FakeIngestionIssueRepository();
        return (new IngestionIssueQueryService(repository), repository);
    }
}
