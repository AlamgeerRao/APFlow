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
    public async Task GetAll_ReturnsPagedResultFromQueryService()
    {
        var listItem = new InvoiceListItemDto(
            Id: InvoiceId, SupplierId: Guid.NewGuid(), SupplierName: "Acme Ltd", SupplierInvoiceNumber: "INV-1",
            InvoiceDate: new DateOnly(2026, 1, 1), DueDate: new DateOnly(2026, 2, 1), Currency: "GBP",
            GrossTotal: 120m, Status: InvoiceStatusCodes.AwaitingReview, CreatedAtUtc: DateTimeOffset.UtcNow,
            IsPotentialDuplicate: false, DuplicateCheckReason: null, DuplicateMatchInvoiceId: null);
        var queryService = new FakeInvoiceQueryService { ResultToReturn = new([listItem], TotalCount: 1, Page: 1, PageSize: 25) };
        var controller = CreateController(new FakeInvoiceService(), new FakeAuditQueryService(), invoiceQueryService: queryService);

        var actionResult = await controller.GetAll(
            status: null, supplierId: null, invoiceDateFrom: null, invoiceDateTo: null, invoiceNumber: null,
            page: 1, pageSize: 25, sortBy: InvoiceSortField.CreatedAtUtc, sortDescending: true, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<PagedResult<InvoiceListItemDto>>(okResult.Value);
        Assert.Equal(1, response.TotalCount);
        var item = Assert.Single(response.Items);
        Assert.Equal(InvoiceId, item.Id);
        Assert.Equal("Acme Ltd", item.SupplierName);
    }

    [Fact]
    public async Task GetAll_DefaultQueryParameters_MatchInvoiceQueryParametersDefaults()
    {
        // The contract the controller must actually honour: ASP.NET Core's
        // parameter defaults when no query string values are supplied must match
        // InvoiceQueryParameters' own defaults exactly (WP-011), not silently
        // diverge from them.
        var queryService = new FakeInvoiceQueryService();
        var controller = CreateController(new FakeInvoiceService(), new FakeAuditQueryService(), invoiceQueryService: queryService);

        await controller.GetAll(
            status: null, supplierId: null, invoiceDateFrom: null, invoiceDateTo: null, invoiceNumber: null,
            page: 1, pageSize: InvoiceQueryParameters.DefaultPageSize, sortBy: InvoiceSortField.CreatedAtUtc, sortDescending: true,
            CancellationToken.None);

        var expectedDefaults = new InvoiceQueryParameters();
        Assert.NotNull(queryService.LastParameters);
        Assert.Equal(expectedDefaults.Page, queryService.LastParameters!.Page);
        Assert.Equal(expectedDefaults.PageSize, queryService.LastParameters.PageSize);
        Assert.Equal(expectedDefaults.SortBy, queryService.LastParameters.SortBy);
        Assert.Equal(expectedDefaults.SortDescending, queryService.LastParameters.SortDescending);
        Assert.Null(queryService.LastParameters.Status);
        Assert.Null(queryService.LastParameters.SupplierId);
    }

    [Fact]
    public async Task GetAll_PassesFilterParametersThroughUnchanged()
    {
        var queryService = new FakeInvoiceQueryService();
        var controller = CreateController(new FakeInvoiceService(), new FakeAuditQueryService(), invoiceQueryService: queryService);
        var supplierId = Guid.NewGuid();

        await controller.GetAll(
            status: InvoiceStatusCodes.NeedsQuery, supplierId: supplierId,
            invoiceDateFrom: new DateOnly(2026, 1, 1), invoiceDateTo: new DateOnly(2026, 1, 31),
            invoiceNumber: "INV", page: 2, pageSize: 10, sortBy: InvoiceSortField.GrossTotal, sortDescending: false,
            CancellationToken.None);

        Assert.NotNull(queryService.LastParameters);
        Assert.Equal(InvoiceStatusCodes.NeedsQuery, queryService.LastParameters!.Status);
        Assert.Equal(supplierId, queryService.LastParameters.SupplierId);
        Assert.Equal(new DateOnly(2026, 1, 1), queryService.LastParameters.InvoiceDateFrom);
        Assert.Equal(new DateOnly(2026, 1, 31), queryService.LastParameters.InvoiceDateTo);
        Assert.Equal("INV", queryService.LastParameters.InvoiceNumber);
        Assert.Equal(2, queryService.LastParameters.Page);
        Assert.Equal(10, queryService.LastParameters.PageSize);
        Assert.Equal(InvoiceSortField.GrossTotal, queryService.LastParameters.SortBy);
        Assert.False(queryService.LastParameters.SortDescending);
    }

    [Fact]
    public async Task GetAll_PassesStatusesArrayThroughUnchanged()
    {
        // WP-074: the Query Queue nav view needs GET /api/invoices to filter on
        // multiple statuses at once, bound from repeated ?statuses= query params -
        // reusing this same endpoint rather than needing a new one.
        var queryService = new FakeInvoiceQueryService();
        var controller = CreateController(new FakeInvoiceService(), new FakeAuditQueryService(), invoiceQueryService: queryService);
        var statuses = new[] { InvoiceStatusCodes.NeedsQuery, InvoiceStatusCodes.QueryRaised, InvoiceStatusCodes.AwaitingSupplierResponse };

        await controller.GetAll(
            status: null, supplierId: null, invoiceDateFrom: null, invoiceDateTo: null, invoiceNumber: null,
            page: 1, pageSize: 25, sortBy: InvoiceSortField.CreatedAtUtc, sortDescending: true,
            CancellationToken.None, statuses: statuses);

        Assert.NotNull(queryService.LastParameters);
        Assert.Equal(statuses, queryService.LastParameters!.Statuses);
    }

    [Fact]
    public async Task GetAll_InvalidPage_ReturnsBadRequestWithCode()
    {
        // Validation is entirely IInvoiceQueryService's own (WP-011) - not
        // duplicated in the controller.
        var queryService = new FakeInvoiceQueryService { FailureToReturn = new Error("InvoiceQuery.InvalidPage", "Page must be 1 or greater.") };
        var controller = CreateController(new FakeInvoiceService(), new FakeAuditQueryService(), invoiceQueryService: queryService);

        var actionResult = await controller.GetAll(
            status: null, supplierId: null, invoiceDateFrom: null, invoiceDateTo: null, invoiceNumber: null,
            page: 0, pageSize: 25, sortBy: InvoiceSortField.CreatedAtUtc, sortDescending: true, CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("InvoiceQuery.InvalidPage", problem.Extensions["code"]);
    }

    [Fact]
    public async Task GetById_ExistingInvoice_ReturnsOkWithInvoiceAndAuditHistory()
    {
        var invoiceService = new FakeInvoiceService
        {
            InvoiceToReturn = NewInvoiceDto(),
            ExtractedFieldsToReturn = [new InvoiceExtractedFieldDto(InvoiceExtractedFieldKeys.SupplierName, "Supplier Name", "Acme Ltd", 0.95)],
        };
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
        var extractedField = Assert.Single(response.ExtractedFields);
        Assert.Equal(InvoiceExtractedFieldKeys.SupplierName, extractedField.FieldKey);
        Assert.Equal(0.95, extractedField.ConfidenceScore);

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

    [Fact]
    public async Task GetNotes_ReturnsNotesFromInvoiceService()
    {
        var invoiceService = new FakeInvoiceService
        {
            NotesToReturn = [new InvoiceNoteDto(Guid.NewGuid(), "A note.", "Priya Shah", DateTimeOffset.UtcNow)],
        };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.GetNotes(InvoiceId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsAssignableFrom<IReadOnlyList<InvoiceNoteDto>>(okResult.Value);
        var note = Assert.Single(response);
        Assert.Equal("A note.", note.Content);
        Assert.Equal("Priya Shah", note.AuthorDisplayName);
    }

    [Fact]
    public async Task GetNotes_UnknownInvoice_ReturnsNotFoundWithCode()
    {
        var invoiceService = new FakeInvoiceService { GetNotesFailureToReturn = new Error("Invoice.NotFound", "not found") };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.GetNotes(InvoiceId, CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("Invoice.NotFound", problem.Extensions["code"]);
    }

    [Fact]
    public async Task CreateNote_ValidContent_ReturnsCreatedWithNoteAndLocationHeader()
    {
        var createdNote = new InvoiceNoteDto(Guid.NewGuid(), "Looks correct.", "Priya Shah", DateTimeOffset.UtcNow);
        var invoiceService = new FakeInvoiceService { AddNoteResultToReturn = createdNote };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.CreateNote(InvoiceId, new CreateInvoiceNoteRequest("Looks correct."), CancellationToken.None);

        var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult);
        Assert.Equal(nameof(InvoicesController.GetNotes), createdResult.ActionName);
        var response = Assert.IsType<InvoiceNoteDto>(createdResult.Value);
        Assert.Equal("Looks correct.", response.Content);
        Assert.Equal("Priya Shah", response.AuthorDisplayName);
        Assert.Equal((InvoiceId, "Looks correct."), invoiceService.LastNoteAdded);
    }

    [Fact]
    public async Task CreateNote_EmptyContent_ReturnsBadRequestWithCode()
    {
        // Reuses IInvoiceService.AddNoteAsync's existing validation - not
        // duplicated in the controller.
        var invoiceService = new FakeInvoiceService
        {
            AddNoteFailureToReturn = new Error("Invoice.InvalidNoteContent", "Note content must not be empty."),
        };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.CreateNote(InvoiceId, new CreateInvoiceNoteRequest(""), CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("Invoice.InvalidNoteContent", problem.Extensions["code"]);
    }

    [Fact]
    public async Task CreateNote_UnknownInvoice_ReturnsNotFoundWithCode()
    {
        var invoiceService = new FakeInvoiceService { AddNoteFailureToReturn = new Error("Invoice.NotFound", "not found") };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.CreateNote(InvoiceId, new CreateInvoiceNoteRequest("A note."), CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status404NotFound, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("Invoice.NotFound", problem.Extensions["code"]);
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
    public async Task UpdateStatus_WithNotes_PassesNotesThroughToUpdateAsync()
    {
        // WP-084: the note is no longer added via a separate, independently-failable
        // IInvoiceService.AddNoteAsync follow-up call after the status change - it's
        // threaded through UpdateInvoiceRequest.Notes so InvoiceService.UpdateAsync
        // can create it atomically with the status change itself, in one commit.
        var invoiceService = new FakeInvoiceService { InvoiceToReturn = NewInvoiceDto(), UpdateResultToReturn = NewInvoiceDto() };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        await controller.UpdateStatus(
            InvoiceId, new UpdateInvoiceStatusRequest(InvoiceStatusCodes.Approved, Notes: "Approved after review"), CancellationToken.None);

        Assert.Equal("Approved after review", invoiceService.LastUpdateRequest?.Notes);
        // Confirms there is no separate AddNoteAsync call any more - the note's
        // only path into the system for this endpoint is through UpdateAsync now.
        Assert.Null(invoiceService.LastNoteAdded);
    }

    [Fact]
    public async Task UpdateStatus_NoNotes_PassesNullNotesThroughToUpdateAsync()
    {
        var invoiceService = new FakeInvoiceService { InvoiceToReturn = NewInvoiceDto(), UpdateResultToReturn = NewInvoiceDto() };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        await controller.UpdateStatus(
            InvoiceId, new UpdateInvoiceStatusRequest(InvoiceStatusCodes.Approved, Notes: null), CancellationToken.None);

        Assert.Null(invoiceService.LastUpdateRequest?.Notes);
        Assert.Null(invoiceService.LastNoteAdded);
    }

    [Fact]
    public async Task UpdateStatus_NoteRequiredForTransitionRejectedByService_ReturnsBadRequest()
    {
        // WP-084: the controller does not duplicate this validation - it's a thin
        // wrapper, same as every other rule InvoiceService.UpdateAsync enforces.
        // This confirms the rejection surfaces correctly as a 400 through the
        // controller's existing ErrorProblem mapping (unmatched codes -> 400).
        var invoiceService = new FakeInvoiceService
        {
            InvoiceToReturn = NewInvoiceDto(),
            UpdateFailureToReturn = new Error("Invoice.NoteRequiredForTransition", "A note is required to change an invoice's status."),
        };
        var controller = CreateController(invoiceService, new FakeAuditQueryService());

        var actionResult = await controller.UpdateStatus(
            InvoiceId, new UpdateInvoiceStatusRequest(InvoiceStatusCodes.Approved, Notes: null), CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("Invoice.NoteRequiredForTransition", problem.Extensions["code"]);
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

    [Fact]
    public async Task GetFolders_ReturnsFolderSummaries()
    {
        var supplierFolderService = new FakeSupplierFolderQueryService
        {
            FoldersToReturn = [new FolderSummaryDto("RECEIVED", "Received", 3), new FolderSummaryDto("AWAITING_REVIEW", "Awaiting Review", 7)],
        };
        var controller = CreateController(
            new FakeInvoiceService(), new FakeAuditQueryService(), supplierFolderQueryService: supplierFolderService);

        var actionResult = await controller.GetFolders(search: "Acme", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var folders = Assert.IsAssignableFrom<IReadOnlyList<FolderSummaryDto>>(okResult.Value);
        Assert.Equal(2, folders.Count);
        Assert.Equal("Acme", supplierFolderService.LastSearch);
    }

    [Fact]
    public async Task GetFolders_ServiceFails_ReturnsProblem()
    {
        var supplierFolderService = new FakeSupplierFolderQueryService { FailureToReturn = new Error("Workflow.TemplateNotFound", "boom") };
        var controller = CreateController(
            new FakeInvoiceService(), new FakeAuditQueryService(), supplierFolderQueryService: supplierFolderService);

        var actionResult = await controller.GetFolders(search: null, CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
    }

    [Fact]
    public async Task GetSuppliers_ReturnsSupplierNames_PassesFolderAndSearchThrough()
    {
        var supplierFolderService = new FakeSupplierFolderQueryService { SuppliersToReturn = ["Acme Ltd", "Zeta Ltd"] };
        var controller = CreateController(
            new FakeInvoiceService(), new FakeAuditQueryService(), supplierFolderQueryService: supplierFolderService);

        var actionResult = await controller.GetSuppliers(folder: "AWAITING_REVIEW", search: "Ac", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var suppliers = Assert.IsAssignableFrom<IReadOnlyList<string>>(okResult.Value);
        Assert.Equal(["Acme Ltd", "Zeta Ltd"], suppliers);
        Assert.Equal(("AWAITING_REVIEW", "Ac"), supplierFolderService.LastSuppliersArgs);
    }

    [Fact]
    public async Task GetGrouped_ReturnsGroupedResult_PassesParametersThrough()
    {
        var supplierFolderService = new FakeSupplierFolderQueryService
        {
            GroupedToReturn = new SupplierGroupedInvoicesDto([], 0, 2, 10),
        };
        var controller = CreateController(
            new FakeInvoiceService(), new FakeAuditQueryService(), supplierFolderQueryService: supplierFolderService);

        var actionResult = await controller.GetGrouped(
            folder: "AWAITING_REVIEW", supplier: "Acme Ltd", search: "Ac", page: 2, pageSize: 10, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var grouped = Assert.IsType<SupplierGroupedInvoicesDto>(okResult.Value);
        Assert.Equal(2, grouped.Page);
        Assert.Equal(10, grouped.PageSize);

        var lastParams = supplierFolderService.LastGroupedParameters;
        Assert.NotNull(lastParams);
        Assert.Equal("AWAITING_REVIEW", lastParams!.Folder);
        Assert.Equal("Acme Ltd", lastParams.Supplier);
        Assert.Equal("Ac", lastParams.Search);
        Assert.Equal(2, lastParams.Page);
        Assert.Equal(10, lastParams.PageSize);
    }

    [Fact]
    public async Task GetGrouped_InvalidPageSize_ReturnsBadRequest()
    {
        var supplierFolderService = new FakeSupplierFolderQueryService
        {
            FailureToReturn = new Error("SupplierFolderQuery.InvalidPageSize", "PageSize must be between 1 and 50."),
        };
        var controller = CreateController(
            new FakeInvoiceService(), new FakeAuditQueryService(), supplierFolderQueryService: supplierFolderService);

        var actionResult = await controller.GetGrouped(
            folder: null, supplier: null, search: null, page: 1, pageSize: 999, CancellationToken.None);

        var problemResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(StatusCodes.Status400BadRequest, problemResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(problemResult.Value);
        Assert.Equal("SupplierFolderQuery.InvalidPageSize", problem.Extensions["code"]);
    }

    private static InvoicesController CreateController(
        FakeInvoiceService invoiceService,
        FakeAuditQueryService auditQueryService,
        FakeAuditService? auditService = null,
        FakeBlobStorageService? blobStorageService = null,
        FakeInvoiceWorkflowActionsService? invoiceWorkflowActionsService = null,
        FakeInvoiceQueryService? invoiceQueryService = null,
        FakeSupplierFolderQueryService? supplierFolderQueryService = null) =>
        new(
            invoiceQueryService ?? new FakeInvoiceQueryService(),
            invoiceService,
            invoiceWorkflowActionsService ?? new FakeInvoiceWorkflowActionsService(),
            supplierFolderQueryService ?? new FakeSupplierFolderQueryService(),
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
        DuplicateMatchInvoiceId: null,
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

        /// <summary>WP-055: what <see cref="GetNotesAsync"/> returns/fails with.</summary>
        public IReadOnlyList<InvoiceNoteDto> NotesToReturn { get; set; } = [];
        public Error? GetNotesFailureToReturn { get; set; }

        /// <summary>WP-055: what <see cref="AddNoteAsync"/> returns/fails with.</summary>
        public InvoiceNoteDto? AddNoteResultToReturn { get; set; }
        public Error? AddNoteFailureToReturn { get; set; }
        public (Guid InvoiceId, string Content)? LastNoteAdded { get; private set; }

        /// <summary>WP-056: what <see cref="GetExtractedFieldsAsync"/> returns/fails with.</summary>
        public IReadOnlyList<InvoiceExtractedFieldDto> ExtractedFieldsToReturn { get; set; } = [];
        public Error? GetExtractedFieldsFailureToReturn { get; set; }

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

        public Task<Result<IReadOnlyList<InvoiceNoteDto>>> GetNotesAsync(Guid invoiceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(GetNotesFailureToReturn is { } error
                ? Result.Failure<IReadOnlyList<InvoiceNoteDto>>(error)
                : Result.Success(NotesToReturn));

        public Task<Result<InvoiceNoteDto>> AddNoteAsync(Guid invoiceId, string content, CancellationToken cancellationToken = default)
        {
            LastNoteAdded = (invoiceId, content);
            return Task.FromResult(AddNoteFailureToReturn is { } error
                ? Result.Failure<InvoiceNoteDto>(error)
                : Result.Success(AddNoteResultToReturn ?? new InvoiceNoteDto(Guid.NewGuid(), content, "Test User", DateTimeOffset.UtcNow)));
        }

        public Task<Result<IReadOnlyList<InvoiceExtractedFieldDto>>> GetExtractedFieldsAsync(Guid invoiceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(GetExtractedFieldsFailureToReturn is { } error
                ? Result.Failure<IReadOnlyList<InvoiceExtractedFieldDto>>(error)
                : Result.Success(ExtractedFieldsToReturn));
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

    /// <summary>WP-058: hand-written fake, same pattern as every fake elsewhere in this codebase.</summary>
    private sealed class FakeInvoiceQueryService : IInvoiceQueryService
    {
        public PagedResult<InvoiceListItemDto> ResultToReturn { get; set; } = new([], 0, 1, InvoiceQueryParameters.DefaultPageSize);
        public Error? FailureToReturn { get; set; }
        public InvoiceQueryParameters? LastParameters { get; private set; }

        public Task<Result<PagedResult<InvoiceListItemDto>>> SearchAsync(InvoiceQueryParameters parameters, CancellationToken cancellationToken = default)
        {
            LastParameters = parameters;
            return Task.FromResult(FailureToReturn is { } error
                ? Result.Failure<PagedResult<InvoiceListItemDto>>(error)
                : Result.Success(ResultToReturn));
        }
    }

    private sealed class FakeSupplierFolderQueryService : ISupplierFolderQueryService
    {
        public IReadOnlyList<FolderSummaryDto> FoldersToReturn { get; set; } = [];
        public IReadOnlyList<string> SuppliersToReturn { get; set; } = [];
        public SupplierGroupedInvoicesDto GroupedToReturn { get; set; } = new([], 0, 1, SupplierFolderQueryParameters.DefaultPageSize);
        public Error? FailureToReturn { get; set; }
        public string? LastSearch { get; private set; }
        public (string? Folder, string? Search)? LastSuppliersArgs { get; private set; }
        public SupplierFolderQueryParameters? LastGroupedParameters { get; private set; }

        public Task<Result<IReadOnlyList<FolderSummaryDto>>> GetFolderCountsAsync(string? search, CancellationToken cancellationToken = default)
        {
            LastSearch = search;
            return Task.FromResult(FailureToReturn is { } error
                ? Result.Failure<IReadOnlyList<FolderSummaryDto>>(error)
                : Result.Success(FoldersToReturn));
        }

        public Task<Result<IReadOnlyList<string>>> GetSupplierNamesAsync(string? folder, string? search, CancellationToken cancellationToken = default)
        {
            LastSuppliersArgs = (folder, search);
            return Task.FromResult(FailureToReturn is { } error
                ? Result.Failure<IReadOnlyList<string>>(error)
                : Result.Success(SuppliersToReturn));
        }

        public Task<Result<SupplierGroupedInvoicesDto>> GetGroupedInvoicesAsync(SupplierFolderQueryParameters parameters, CancellationToken cancellationToken = default)
        {
            LastGroupedParameters = parameters;
            return Task.FromResult(FailureToReturn is { } error
                ? Result.Failure<SupplierGroupedInvoicesDto>(error)
                : Result.Success(GroupedToReturn));
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
