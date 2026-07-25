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
    public async Task GetInvoices_ReturnsPagedResultFromQueryService()
    {
        var queryService = new FakeInvoiceQueryService
        {
            ResultToReturn = new PagedResult<InvoiceListItemDto>([NewInvoiceListItemDto()], 1, 1, 25),
        };
        var controller = CreateController(new FakeInvoiceService(), new FakeAuditQueryService(), invoiceQueryService: queryService);

        var actionResult = await controller.GetInvoices(search: "acme", status: null, page: 1, pageSize: 25, cancellationToken: CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<PagedResult<InvoiceListItemDto>>(okResult.Value);
        Assert.Equal(1, response.TotalCount);
        Assert.Equal("acme", queryService.LastParameters?.Search);
    }

    [Fact]
    public async Task GetInvoices_SortDirectionAsc_MapsToSortDescendingFalse()
    {
        var queryService = new FakeInvoiceQueryService();
        var controller = CreateController(new FakeInvoiceService(), new FakeAuditQueryService(), invoiceQueryService: queryService);

        await controller.GetInvoices(search: null, status: null, sortDirection: "asc", cancellationToken: CancellationToken.None);

        Assert.False(queryService.LastParameters?.SortDescending);
    }

    [Fact]
    public async Task GetInvoices_InvalidQuery_ReturnsBadRequestWithCode()
    {
        var queryService = new FakeInvoiceQueryService { FailureToReturn = new Error("InvoiceQuery.InvalidPageSize", "bad") };
        var controller = CreateController(new FakeInvoiceService(), new FakeAuditQueryService(), invoiceQueryService: queryService);

        var actionResult = await controller.GetInvoices(search: null, status: null, cancellationToken: CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("InvoiceQuery.InvalidPageSize", problem.Extensions["code"]);
    }

    [Fact]
    public async Task GetNotes_ExistingInvoice_ReturnsOkWithNotes()
    {
        var invoiceService = new FakeInvoiceService
        {
            NotesToReturn = [new InvoiceNoteDto(Guid.NewGuid(), "First note.", "Priya Shah", DateTimeOffset.UtcNow)],
        };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.GetNotes(InvoiceId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var notes = Assert.IsAssignableFrom<IReadOnlyList<InvoiceNoteDto>>(okResult.Value);
        Assert.Single(notes);
        Assert.Equal("Priya Shah", notes[0].AuthorDisplayName);
    }

    [Fact]
    public async Task GetNotes_UnknownInvoice_ReturnsNotFound()
    {
        var invoiceService = new FakeInvoiceService { GetNotesFailureToReturn = new Error("Invoice.NotFound", "not found") };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.GetNotes(InvoiceId, CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
    }

    [Fact]
    public async Task AddNote_ValidContent_ReturnsCreatedWithNoteDto()
    {
        var expectedNote = new InvoiceNoteDto(Guid.NewGuid(), "Approved after review.", "Priya Shah", DateTimeOffset.UtcNow);
        var invoiceService = new FakeInvoiceService { AddNoteResultToReturn = expectedNote };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.AddNote(InvoiceId, new CreateInvoiceNoteRequest("Approved after review."), CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult);
        var note = Assert.IsType<InvoiceNoteDto>(createdResult.Value);
        Assert.Equal(expectedNote.Id, note.Id);
        Assert.Equal(nameof(InvoicesController.GetNotes), createdResult.ActionName);
        Assert.Equal((InvoiceId, "Approved after review."), invoiceService.LastNoteAdded);
    }

    [Fact]
    public async Task AddNote_EmptyContent_ReturnsBadRequestWithCode()
    {
        var invoiceService = new FakeInvoiceService { AddNoteFailureToReturn = new Error("Invoice.InvalidNoteContent", "empty") };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.AddNote(InvoiceId, new CreateInvoiceNoteRequest(""), CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("Invoice.InvalidNoteContent", problem.Extensions["code"]);
    }

    [Fact]
    public async Task AddNote_UnknownInvoice_ReturnsNotFoundWithCode()
    {
        var invoiceService = new FakeInvoiceService { AddNoteFailureToReturn = new Error("Invoice.NotFound", "not found") };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.AddNote(InvoiceId, new CreateInvoiceNoteRequest("test"), CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
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

    [Fact]
    public async Task GetAvailableActions_ReturnsActionsFromWorkflowActionsService()
    {
        var actionsService = new FakeInvoiceWorkflowActionsService
        {
            ActionsToReturn = [new AvailableActionDto(InvoiceStatusCodes.Approved, "Approved")],
        };
        var controller = CreateController(new FakeInvoiceService(), new FakeAuditQueryService(), invoiceWorkflowActionsService: actionsService);

        var actionResult = await controller.GetAvailableActions(InvoiceId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsAssignableFrom<IReadOnlyList<AvailableActionResponse>>(okResult.Value);
        var action = Assert.Single(response);
        Assert.Equal(InvoiceStatusCodes.Approved, action.TargetStatusCode);
        Assert.Equal("Approved", action.TargetStatusLabel);
        Assert.Equal(InvoiceId, actionsService.LastRequestedInvoiceId);
    }

    [Fact]
    public async Task GetAvailableActions_UnknownInvoice_ReturnsNotFoundWithCode()
    {
        var actionsService = new FakeInvoiceWorkflowActionsService { FailureToReturn = new Error("Invoice.NotFound", "not found") };
        var controller = CreateController(new FakeInvoiceService(), new FakeAuditQueryService(), invoiceWorkflowActionsService: actionsService);

        var actionResult = await controller.GetAvailableActions(InvoiceId, CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("Invoice.NotFound", problem.Extensions["code"]);
    }

    [Fact]
    public async Task UpdateStatus_ValidTransition_ReturnsOkWithInvoiceDetail_AndPreservesOtherFields()
    {
        var currentInvoice = NewInvoiceDto();
        var updatedInvoice = currentInvoice with { Status = InvoiceStatusCodes.Approved };
        var invoiceService = new FakeInvoiceService { InvoiceToReturn = currentInvoice, UpdateResultToReturn = updatedInvoice };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.UpdateStatus(
            InvoiceId, new UpdateInvoiceStatusRequest(InvoiceStatusCodes.Approved, Notes: null), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<InvoiceDetailResponse>(okResult.Value);
        Assert.Equal(InvoiceStatusCodes.Approved, response.Invoice.Status);

        // Every other editable field was resubmitted unchanged - UpdateAsync's
        // contract is a full field replace, not a partial patch.
        Assert.NotNull(invoiceService.LastUpdateRequest);
        Assert.Equal(currentInvoice.SupplierInvoiceNumber, invoiceService.LastUpdateRequest!.SupplierInvoiceNumber);
        Assert.Equal(currentInvoice.Currency, invoiceService.LastUpdateRequest.Currency);
        Assert.Equal(currentInvoice.GrossTotal, invoiceService.LastUpdateRequest.GrossTotal);
        Assert.Equal(InvoiceStatusCodes.Approved, invoiceService.LastUpdateRequest.Status);
    }

    [Fact]
    public async Task UpdateStatus_WithNotes_AddsNoteAfterSuccessfulStatusChange()
    {
        var invoiceService = new FakeInvoiceService { InvoiceToReturn = NewInvoiceDto(), UpdateResultToReturn = NewInvoiceDto() };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        await controller.UpdateStatus(
            InvoiceId, new UpdateInvoiceStatusRequest(InvoiceStatusCodes.Approved, Notes: "Approved after review"), CancellationToken.None);

        Assert.Equal((InvoiceId, "Approved after review"), invoiceService.LastNoteAdded);
    }

    [Fact]
    public async Task UpdateStatus_NoNotes_DoesNotCallAddNoteAsync()
    {
        var invoiceService = new FakeInvoiceService { InvoiceToReturn = NewInvoiceDto(), UpdateResultToReturn = NewInvoiceDto() };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        await controller.UpdateStatus(
            InvoiceId, new UpdateInvoiceStatusRequest(InvoiceStatusCodes.Approved, Notes: null), CancellationToken.None);

        Assert.Null(invoiceService.LastNoteAdded);
    }

    [Fact]
    public async Task UpdateStatus_UnknownInvoice_ReturnsNotFoundWithCode()
    {
        var invoiceService = new FakeInvoiceService { FailureToReturn = new Error("Invoice.NotFound", "not found") };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.UpdateStatus(
            InvoiceId, new UpdateInvoiceStatusRequest(InvoiceStatusCodes.Approved, Notes: null), CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("Invoice.NotFound", problem.Extensions["code"]);
    }

    [Fact]
    public async Task UpdateStatus_TransitionNotAllowed_ReturnsBadRequestWithCode()
    {
        var invoiceService = new FakeInvoiceService
        {
            InvoiceToReturn = NewInvoiceDto(),
            UpdateFailureToReturn = new Error("Workflow.TransitionNotAllowed", "not an allowed transition"),
        };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.UpdateStatus(
            InvoiceId, new UpdateInvoiceStatusRequest(InvoiceStatusCodes.Paid, Notes: null), CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("Workflow.TransitionNotAllowed", problem.Extensions["code"]);
    }

    [Fact]
    public async Task UpdateStatus_RoleNotPermitted_ReturnsForbiddenWithCode()
    {
        // The real code IApprovalAuthorizationService produces for "your role
        // doesn't satisfy this gated transition's policy" is Approval.Unauthorized
        // - see ErrorProblem's own doc comment for why this endpoint maps that
        // (not an invented "Workflow.RoleNotPermitted") to 403.
        var invoiceService = new FakeInvoiceService
        {
            InvoiceToReturn = NewInvoiceDto(),
            UpdateFailureToReturn = new Error("Approval.Unauthorized", "This action requires the 'FINANCE_MANAGER' role."),
        };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.UpdateStatus(
            InvoiceId, new UpdateInvoiceStatusRequest(InvoiceStatusCodes.Approved, Notes: null), CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status403Forbidden, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("Approval.Unauthorized", problem.Extensions["code"]);
    }

    private static InvoicesController CreateController(
        FakeInvoiceService invoiceService,
        FakeAuditQueryService auditQueryService,
        FakeAuditService? auditService = null,
        FakeBlobStorageService? blobStorageService = null,
        FakeInvoiceWorkflowActionsService? invoiceWorkflowActionsService = null,
        FakeInvoiceQueryService? invoiceQueryService = null) =>
        new(
            invoiceService,
            invoiceQueryService ?? new FakeInvoiceQueryService(),
            invoiceWorkflowActionsService ?? new FakeInvoiceWorkflowActionsService(),
            auditQueryService,
            auditService ?? new FakeAuditService(),
            blobStorageService ?? new FakeBlobStorageService(),
            NullLogger<InvoicesController>.Instance);

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

    private static InvoiceListItemDto NewInvoiceListItemDto() => new(
        Id: Guid.NewGuid(),
        SupplierId: Guid.NewGuid(),
        SupplierName: "Acme Ltd",
        SupplierInvoiceNumber: "INV-1",
        InvoiceDate: new DateOnly(2026, 1, 1),
        DueDate: new DateOnly(2026, 2, 1),
        Currency: "GBP",
        GrossTotal: 120m,
        Status: InvoiceStatusCodes.Extracted,
        CreatedAtUtc: DateTimeOffset.UtcNow,
        IsPotentialDuplicate: false,
        DuplicateCheckReason: null);

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

        /// <summary>
        /// WP-054: what <see cref="UpdateAsync"/> returns/fails with, kept
        /// separate from <see cref="FailureToReturn"/> (which governs
        /// <see cref="GetByIdAsync"/>) so a test can make the initial lookup
        /// succeed but the update itself fail (e.g. a rejected transition), or
        /// vice versa.
        /// </summary>
        public InvoiceDto? UpdateResultToReturn { get; set; }
        public Error? UpdateFailureToReturn { get; set; }
        public UpdateInvoiceRequest? LastUpdateRequest { get; private set; }

        public Error? AddNoteFailureToReturn { get; set; }
        public (Guid InvoiceId, string Content)? LastNoteAdded { get; private set; }
        public InvoiceNoteDto? AddNoteResultToReturn { get; set; }

        public IReadOnlyList<InvoiceNoteDto> NotesToReturn { get; set; } = [];
        public Error? GetNotesFailureToReturn { get; set; }

        public Task<Result<InvoiceDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(FailureToReturn is { } error ? Result.Failure<InvoiceDto>(error) : Result.Success(InvoiceToReturn!));

        public Task<Result<IReadOnlyList<InvoiceDto>>> GetAllAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by InvoicesController.");

        public Task<Result<InvoiceDto>> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by InvoicesController.");

        public Task<Result<InvoiceDto>> UpdateAsync(Guid id, UpdateInvoiceRequest request, CancellationToken cancellationToken = default)
        {
            LastUpdateRequest = request;
            return Task.FromResult(UpdateFailureToReturn is { } error
                ? Result.Failure<InvoiceDto>(error)
                : Result.Success(UpdateResultToReturn ?? InvoiceToReturn!));
        }

        public Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("Not used by InvoicesController.");

        public Task<Result<InvoiceNoteDto>> AddNoteAsync(Guid invoiceId, string content, CancellationToken cancellationToken = default)
        {
            LastNoteAdded = (invoiceId, content);
            return Task.FromResult(AddNoteFailureToReturn is { } error
                ? Result.Failure<InvoiceNoteDto>(error)
                : Result.Success(AddNoteResultToReturn ?? new InvoiceNoteDto(Guid.NewGuid(), content, "Test User", DateTimeOffset.UtcNow)));
        }

        public Task<Result<IReadOnlyList<InvoiceNoteDto>>> GetNotesAsync(Guid invoiceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(GetNotesFailureToReturn is { } error
                ? Result.Failure<IReadOnlyList<InvoiceNoteDto>>(error)
                : Result.Success(NotesToReturn));
    }

    private sealed class FakeInvoiceQueryService : IInvoiceQueryService
    {
        public PagedResult<InvoiceListItemDto>? ResultToReturn { get; set; }
        public Error? FailureToReturn { get; set; }
        public InvoiceQueryParameters? LastParameters { get; private set; }

        public Task<Result<PagedResult<InvoiceListItemDto>>> SearchAsync(InvoiceQueryParameters parameters, CancellationToken cancellationToken = default)
        {
            LastParameters = parameters;
            return Task.FromResult(FailureToReturn is { } error
                ? Result.Failure<PagedResult<InvoiceListItemDto>>(error)
                : Result.Success(ResultToReturn ?? new PagedResult<InvoiceListItemDto>([], 0, parameters.Page, parameters.PageSize)));
        }
    }

    /// <summary>Hand-written fake, same pattern as every fake elsewhere in this codebase.</summary>
    private sealed class FakeInvoiceWorkflowActionsService : IInvoiceWorkflowActionsService
    {
        public IReadOnlyList<AvailableActionDto> ActionsToReturn { get; set; } = [];
        public Error? FailureToReturn { get; set; }
        public Guid? LastRequestedInvoiceId { get; private set; }

        public Task<Result<IReadOnlyList<AvailableActionDto>>> GetAvailableActionsAsync(Guid invoiceId, CancellationToken cancellationToken = default)
        {
            LastRequestedInvoiceId = invoiceId;
            return Task.FromResult(FailureToReturn is { } error
                ? Result.Failure<IReadOnlyList<AvailableActionDto>>(error)
                : Result.Success(ActionsToReturn));
        }
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
