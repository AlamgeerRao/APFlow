using APFlow.Application.DTOs;
using APFlow.Application.Features.Audit;
using APFlow.Application.Tests.Features;
using APFlow.Domain.Entities;
using Xunit;

namespace APFlow.Application.Tests.Features.Audit;

public class AuditQueryServiceTests
{
    [Fact]
    public async Task SearchAsync_FiltersByEntityNameAndEntityId()
    {
        var (service, repository) = CreateService();
        var targetInvoiceId = Guid.NewGuid();
        repository.AuditLogs.Add(NewEntry(entityName: "Invoice", entityId: targetInvoiceId));
        repository.AuditLogs.Add(NewEntry(entityName: "Invoice", entityId: Guid.NewGuid()));
        repository.AuditLogs.Add(NewEntry(entityName: "Supplier", entityId: targetInvoiceId));

        var result = await service.SearchAsync(new AuditLogQueryParameters(EntityName: "Invoice", EntityId: targetInvoiceId));

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(targetInvoiceId, item.EntityId);
        Assert.Equal("Invoice", item.EntityName);
    }

    [Fact]
    public async Task SearchAsync_NoFilters_ReturnsAllEntries()
    {
        var (service, repository) = CreateService();
        repository.AuditLogs.Add(NewEntry());
        repository.AuditLogs.Add(NewEntry());

        var result = await service.SearchAsync(new AuditLogQueryParameters());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalCount);
    }

    [Fact]
    public async Task SearchAsync_AppliesPaging()
    {
        var (service, repository) = CreateService();
        for (var i = 0; i < 5; i++)
        {
            repository.AuditLogs.Add(NewEntry());
        }

        var page1 = await service.SearchAsync(new AuditLogQueryParameters(Page: 1, PageSize: 2));
        var page2 = await service.SearchAsync(new AuditLogQueryParameters(Page: 2, PageSize: 2));

        Assert.True(page1.IsSuccess);
        Assert.Equal(5, page1.Value.TotalCount);
        Assert.Equal(3, page1.Value.TotalPages);
        Assert.Equal(2, page1.Value.Items.Count);
        Assert.Equal(2, page2.Value.Items.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task SearchAsync_InvalidPage_ReturnsFailure(int page)
    {
        var (service, _) = CreateService();

        var result = await service.SearchAsync(new AuditLogQueryParameters(Page: page));

        Assert.True(result.IsFailure);
        Assert.Equal("AuditLogQuery.InvalidPage", result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public async Task SearchAsync_InvalidPageSize_ReturnsFailure(int pageSize)
    {
        var (service, _) = CreateService();

        var result = await service.SearchAsync(new AuditLogQueryParameters(PageSize: pageSize));

        Assert.True(result.IsFailure);
        Assert.Equal("AuditLogQuery.InvalidPageSize", result.Error.Code);
    }

    [Fact]
    public async Task SearchAsync_FromUtcAfterToUtc_ReturnsFailure()
    {
        var (service, _) = CreateService();

        var result = await service.SearchAsync(new AuditLogQueryParameters(
            FromUtc: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            ToUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.True(result.IsFailure);
        Assert.Equal("AuditLogQuery.InvalidDateRange", result.Error.Code);
    }

    private static AuditLog NewEntry(string entityName = "Invoice", Guid? entityId = null) => new()
    {
        Action = "InvoiceStatusChanged",
        EntityName = entityName,
        EntityId = entityId ?? Guid.NewGuid(),
        PreviousValue = "Received",
        NewValue = "Extracted",
    };

    private static (AuditQueryService Service, FakeAuditLogRepository Repository) CreateService()
    {
        var repository = new FakeAuditLogRepository();
        return (new AuditQueryService(repository), repository);
    }
}
