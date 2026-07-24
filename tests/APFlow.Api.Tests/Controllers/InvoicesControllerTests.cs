using APFlow.Api.Contracts;
using APFlow.Api.Controllers;
using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Common.Constants;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace APFlow.Api.Tests.Controllers;

public class InvoicesControllerTests
{
    private static readonly Guid InvoiceId = Guid.NewGuid();

    [Fact]
    public async Task GetById_ExistingInvoice_ReturnsOkWithInvoiceAndAuditHistory()
    {
        var invoiceService = new FakeInvoiceService { InvoiceToReturn = NewInvoiceDto() };
        var auditQueryService = new FakeAuditQueryService
        {
            ItemsToReturn = [NewAuditLogDto(AuditActions.InvoiceCreated), NewAuditLogDto(AuditActions.InvoiceStatusChanged)],
        };
        var controller = CreateController(invoiceService, auditQueryService);

        var actionResult = await controller.GetById(InvoiceId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<InvoiceDetailResponse>(okResult.Value);
        Assert.Equal(InvoiceId, response.Invoice.Id);
        Assert.Equal(2, response.RecentAuditEntries.Count);
        Assert.False(string.IsNullOrWhiteSpace(response.ExtractionConfidenceNote));

        // The query was scoped to this specific invoice.
        Assert.Equal(nameof(APFlow.Domain.Entities.Invoice), auditQueryService.LastParameters?.EntityName);
        Assert.Equal(InvoiceId, auditQueryService.LastParameters?.EntityId);
    }

    [Fact]
    public async Task GetById_UnknownInvoice_ReturnsNotFound()
    {
        var invoiceService = new FakeInvoiceService { FailureToReturn = new Error("Invoice.NotFound", "not found") };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.GetById(InvoiceId, CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("Invoice.NotFound", problem.Title);
    }

    [Fact]
    public async Task GetById_AuditQueryFails_StillReturnsInvoiceWithEmptyHistory()
    {
        var invoiceService = new FakeInvoiceService { InvoiceToReturn = NewInvoiceDto() };
        var auditQueryService = new FakeAuditQueryService { FailureToReturn = new Error("AuditLogQuery.InvalidPage", "boom") };
        var controller = CreateController(invoiceService, auditQueryService);

        var actionResult = await controller.GetById(InvoiceId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<InvoiceDetailResponse>(okResult.Value);
        Assert.Empty(response.RecentAuditEntries);
    }

    [Fact]
    public async Task Download_ExistingInvoiceWithDocument_StreamsFileAndStagesDocumentViewedAuditEntry()
    {
        var invoiceService = new FakeInvoiceService { InvoiceToReturn = NewInvoiceDto(blobName: "invoices/msg-1/invoice.pdf") };
        var blobStorageService = new FakeBlobStorageService { StreamToReturn = new MemoryStream([1, 2, 3]) };
        var auditService = new FakeAuditService();
        var controller = CreateController(invoiceService, new FakeAuditQueryService(), auditService, blobStorageService);

        var actionResult = await controller.Download(InvoiceId, CancellationToken.None);

        var fileResult = Assert.IsType<FileStreamResult>(actionResult);
        Assert.Equal("application/pdf", fileResult.ContentType);
        Assert.Equal("invoices/msg-1/invoice.pdf", blobStorageService.LastRequestedBlobName);

        // The required audit-logging side effect: staged (and, since this is a
        // read-only GET with nothing else to commit alongside, immediately saved)
        // via LogAndSaveAsync specifically, not LogAsync.
        var entry = Assert.Single(auditService.LoggedAndSavedRequests);
        Assert.Equal(AuditActions.DocumentViewed, entry.Action);
        Assert.Equal(nameof(APFlow.Domain.Entities.Invoice), entry.EntityName);
        Assert.Equal(InvoiceId, entry.EntityId);
        Assert.Empty(auditService.StagedOnlyRequests); // confirms LogAsync (stage-only) was NOT used here
    }

    [Fact]
    public async Task Download_UnknownInvoice_ReturnsNotFound_NoAuditEntryStaged()
    {
        var invoiceService = new FakeInvoiceService { FailureToReturn = new Error("Invoice.NotFound", "not found") };
        var auditService = new FakeAuditService();
        var controller = CreateController(invoiceService, new FakeAuditQueryService(), auditService);

        var actionResult = await controller.Download(InvoiceId, CancellationToken.None);

        Assert.IsType<ObjectResult>(actionResult);
        Assert.Empty(auditService.LoggedAndSavedRequests);
    }

    [Fact]
    public async Task Download_InvoiceWithNoSourceDocument_ReturnsNotFound()
    {
        var invoiceService = new FakeInvoiceService { InvoiceToReturn = NewInvoiceDto(blobName: null) };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.Download(InvoiceId, CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("Invoice.NoSourceDocument", problem.Title);
    }

    [Fact]
    public async Task Download_BlobDownloadFails_ReturnsNotFound_NoAuditEntryStaged()
    {
        var invoiceService = new FakeInvoiceService { InvoiceToReturn = NewInvoiceDto(blobName: "invoices/msg-1/invoice.pdf") };
        var blobStorageService = new FakeBlobStorageService { FailureToReturn = new Error("BlobStorage.NotFound", "missing") };
        var auditService = new FakeAuditService();
        var controller = CreateController(invoiceService, new FakeAuditQueryService(), auditService, blobStorageService);

        var actionResult = await controller.Download(InvoiceId, CancellationToken.None);

        Assert.IsType<ObjectResult>(actionResult);
        Assert.Empty(auditService.LoggedAndSavedRequests);
    }

    [Fact]
    public async Task Download_AuditStagingFails_DocumentStillReturned()
    {
        var invoiceService = new FakeInvoiceService { InvoiceToReturn = NewInvoiceDto(blobName: "invoices/msg-1/invoice.pdf") };
        var blobStorageService = new FakeBlobStorageService { StreamToReturn = new MemoryStream([1, 2, 3]) };
        var auditService = new FakeAuditService { FailureToReturn = new Error("Approval.PolicyNotConfigured", "irrelevant here, just simulating failure") };
        var controller = CreateController(invoiceService, new FakeAuditQueryService(), auditService, blobStorageService);

        var actionResult = await controller.Download(InvoiceId, CancellationToken.None);

        // The document is still returned even though the audit entry failed - a
        // missing audit entry does not block an already-authorized document view.
        Assert.IsType<FileStreamResult>(actionResult);
    }

    private static InvoicesController CreateController(
        FakeInvoiceService invoiceService,
        FakeAuditQueryService auditQueryService,
        FakeAuditService? auditService = null,
        FakeBlobStorageService? blobStorageService = null) =>
        new(invoiceService, auditQueryService, auditService ?? new FakeAuditService(), blobStorageService ?? new FakeBlobStorageService(), NullLogger<InvoicesController>.Instance);

    private static InvoiceDto NewInvoiceDto(string? blobName = "invoices/msg-1/invoice.pdf") => new(
        Id: InvoiceId,
        SupplierId: Guid.NewGuid(),
        SupplierName: "Acme Ltd",
        SupplierInvoiceNumber: "INV-1",
        InvoiceDate: new DateOnly(2026, 1, 1),
        DueDate: new DateOnly(2026, 2, 1),
        Currency: "GBP",
        NetAmount: 100m,
        Vat: 20m,
        GrossTotal: 120m,
        Status: InvoiceStatusCodes.Extracted,
        SourceEmailMessageId: "msg-1",
        SourceDocumentBlobName: blobName,
        SourceDocumentContentHash: "abc123",
        IsPotentialDuplicate: false,
        DuplicateCheckReason: null,
        CreatedAtUtc: DateTimeOffset.UtcNow);

    private static AuditLogDto NewAuditLogDto(string action) => new(
        Id: Guid.NewGuid(),
        PerformedByUserId: "test-user",
        Action: action,
        EntityName: "Invoice",
        EntityId: InvoiceId,
        PreviousValue: null,
        NewValue: null,
        PerformedAtUtc: DateTimeOffset.UtcNow);

    private sealed class FakeInvoiceService : IInvoiceService
    {
        public InvoiceDto? InvoiceToReturn { get; set; }
        public Error? FailureToReturn { get; set; }

        public Task<Result<InvoiceDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(FailureToReturn is { } error ? Result.Failure<InvoiceDto>(error) : Result.Success(InvoiceToReturn!));

        public Task<Result<IReadOnlyList<InvoiceDto>>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by InvoicesController.");

        public Task<Result<InvoiceDto>> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by InvoicesController.");

        public Task<Result<InvoiceDto>> UpdateAsync(Guid id, UpdateInvoiceRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by InvoicesController.");

        public Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by InvoicesController.");

        public Task<Result> AddNoteAsync(Guid invoiceId, string content, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by InvoicesController.");
    }

    private sealed class FakeAuditQueryService : IAuditQueryService
    {
        public IReadOnlyList<AuditLogDto> ItemsToReturn { get; set; } = [];
        public Error? FailureToReturn { get; set; }
        public AuditLogQueryParameters? LastParameters { get; private set; }

        public Task<Result<PagedResult<AuditLogDto>>> SearchAsync(AuditLogQueryParameters parameters, CancellationToken cancellationToken = default)
        {
            LastParameters = parameters;
            return Task.FromResult(FailureToReturn is { } error
                ? Result.Failure<PagedResult<AuditLogDto>>(error)
                : Result.Success(new PagedResult<AuditLogDto>(ItemsToReturn, ItemsToReturn.Count, parameters.Page, parameters.PageSize)));
        }
    }

    private sealed class FakeAuditService : IAuditService
    {
        public Error? FailureToReturn { get; set; }
        public List<RecordAuditLogRequest> StagedOnlyRequests { get; } = [];
        public List<RecordAuditLogRequest> LoggedAndSavedRequests { get; } = [];

        public Task<Result<Guid>> LogAsync(RecordAuditLogRequest request, CancellationToken cancellationToken = default)
        {
            StagedOnlyRequests.Add(request);
            return Task.FromResult(FailureToReturn is { } error ? Result.Failure<Guid>(error) : Result.Success(Guid.NewGuid()));
        }

        public Task<Result<Guid>> LogAndSaveAsync(RecordAuditLogRequest request, CancellationToken cancellationToken = default)
        {
            if (FailureToReturn is { } error)
            {
                return Task.FromResult(Result.Failure<Guid>(error));
            }

            LoggedAndSavedRequests.Add(request);
            return Task.FromResult(Result.Success(Guid.NewGuid()));
        }
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        public Stream? StreamToReturn { get; set; }
        public Error? FailureToReturn { get; set; }
        public string? LastRequestedBlobName { get; private set; }

        public Task<Result<string>> UploadAsync(string blobName, Stream content, string? contentType = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by InvoicesController.");

        public Task<Result<Stream>> DownloadAsync(string blobName, CancellationToken cancellationToken = default)
        {
            LastRequestedBlobName = blobName;
            return Task.FromResult(FailureToReturn is { } error ? Result.Failure<Stream>(error) : Result.Success(StreamToReturn!));
        }

        public Task<Result> DeleteAsync(string blobName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by InvoicesController.");

        public Task<Result<Uri>> GenerateSasUrlAsync(string blobName, TimeSpan validFor, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by InvoicesController.");

        public Task<bool> VerifyContainerAccessAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by InvoicesController.");
    }
}
