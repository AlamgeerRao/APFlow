using APFlow.Application.DTOs;
using APFlow.Application.Features.Audit;
using APFlow.Application.Tests.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace APFlow.Application.Tests.Features.Audit;

public class AuditServiceTests
{
    [Fact]
    public async Task LogAsync_ValidRequest_StagesEntry_DoesNotCallSaveChanges()
    {
        var (service, repository) = CreateService();

        var result = await service.LogAsync(new RecordAuditLogRequest(
            "InvoiceStatusChanged", "Invoice", Guid.NewGuid(), "Received", "Extracted"));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(repository.AuditLogs);
        Assert.Equal(result.Value, entry.Id);
        Assert.Equal("InvoiceStatusChanged", entry.Action);
        Assert.Equal("Invoice", entry.EntityName);
        Assert.Equal("Received", entry.PreviousValue);
        Assert.Equal("Extracted", entry.NewValue);

        // The whole point of the "stage, don't save" design (see IAuditService.LogAsync's
        // doc comment) - LogAsync itself never calls SaveChangesAsync.
        Assert.False(repository.SaveChangesCalled);
    }

    [Fact]
    public async Task LogAsync_NullPreviousAndNewValue_Succeeds()
    {
        var (service, repository) = CreateService();

        var result = await service.LogAsync(new RecordAuditLogRequest(
            "InvoiceCreated", "Invoice", Guid.NewGuid(), null, null));

        Assert.True(result.IsSuccess);
        var entry = Assert.Single(repository.AuditLogs);
        Assert.Null(entry.PreviousValue);
        Assert.Null(entry.NewValue);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task LogAsync_BlankAction_ReturnsFailure(string? action)
    {
        var (service, repository) = CreateService();

        var result = await service.LogAsync(new RecordAuditLogRequest(action!, "Invoice", Guid.NewGuid(), null, null));

        Assert.True(result.IsFailure);
        Assert.Equal("AuditLog.InvalidAction", result.Error.Code);
        Assert.Empty(repository.AuditLogs);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task LogAsync_BlankEntityName_ReturnsFailure(string? entityName)
    {
        var (service, repository) = CreateService();

        var result = await service.LogAsync(new RecordAuditLogRequest("Action", entityName!, Guid.NewGuid(), null, null));

        Assert.True(result.IsFailure);
        Assert.Equal("AuditLog.InvalidEntityName", result.Error.Code);
    }

    [Fact]
    public async Task LogAsync_EmptyEntityId_ReturnsFailure()
    {
        var (service, _) = CreateService();

        var result = await service.LogAsync(new RecordAuditLogRequest("Action", "Invoice", Guid.Empty, null, null));

        Assert.True(result.IsFailure);
        Assert.Equal("AuditLog.InvalidEntityId", result.Error.Code);
    }

    [Fact]
    public async Task LogAsync_ActionTooLong_ReturnsFailure()
    {
        var (service, _) = CreateService();

        var result = await service.LogAsync(new RecordAuditLogRequest(
            new string('a', 101), "Invoice", Guid.NewGuid(), null, null));

        Assert.True(result.IsFailure);
        Assert.Equal("AuditLog.InvalidAction", result.Error.Code);
    }

    [Fact]
    public async Task LogAsync_PreviousValueTooLong_ReturnsFailure()
    {
        var (service, _) = CreateService();

        var result = await service.LogAsync(new RecordAuditLogRequest(
            "Action", "Invoice", Guid.NewGuid(), new string('a', 2001), null));

        Assert.True(result.IsFailure);
        Assert.Equal("AuditLog.InvalidPreviousValue", result.Error.Code);
    }

    private static (AuditService Service, FakeAuditLogRepository Repository) CreateService()
    {
        var repository = new FakeAuditLogRepository();
        return (new AuditService(repository, NullLogger<AuditService>.Instance), repository);
    }
}
