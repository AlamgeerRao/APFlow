using APFlow.Api.Contracts;
using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace APFlow.Api.Controllers;

/// <summary>
/// Invoice read/document/status-transition/notes endpoints (WP-052 Part D;
/// status transitions added WP-054; notes added WP-055). The solution-wide
/// fallback authorization policy (see
/// <c>AuthorizationExtensions.AddApiAuthorization</c>) already requires an
/// authenticated caller for every action here - no <c>[AllowAnonymous]</c>
/// anywhere in this controller. Tenant isolation and transition/role enforcement
/// are entirely owned by the underlying services this controller composes
/// (<see cref="IInvoiceService"/>'s repository query filter and (WP-053)
/// live <c>IWorkflowValidationService</c>/<c>IApprovalAuthorizationService</c>
/// gating inside <c>InvoiceService.UpdateAsync</c>,
/// <see cref="IInvoiceWorkflowActionsService"/>'s equivalent read-side
/// filtering, <see cref="IBlobStorageService"/>'s tenant-prefixed blob names) -
/// this controller adds no business logic of its own, matching
/// "APFlow.Api ... contains no business logic" (Solution Structure §1/§5). The
/// WP-054 and WP-055 endpoints are all thin HTTP wrappers: they call an
/// existing Application-layer service directly and translate the
/// <see cref="Result"/> outcome into an HTTP response - no new business rule is
/// introduced in this controller.
/// </summary>
[ApiController]
[Route("api/invoices")]
public sealed class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IInvoiceWorkflowActionsService _invoiceWorkflowActionsService;
    private readonly IAuditQueryService _auditQueryService;
    private readonly IAuditService _auditService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<InvoicesController> _logger;

    /// <summary>Creates a new <see cref="InvoicesController"/>.</summary>
    public InvoicesController(
        IInvoiceService invoiceService,
        IInvoiceWorkflowActionsService invoiceWorkflowActionsService,
        IAuditQueryService auditQueryService,
        IAuditService auditService,
        IBlobStorageService blobStorageService,
        ILogger<InvoicesController> logger)
    {
        _invoiceService = invoiceService;
        _invoiceWorkflowActionsService = invoiceWorkflowActionsService;
        _auditQueryService = auditQueryService;
        _auditService = auditService;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    /// <summary>
    /// Returns canonical invoice/supplier fields, duplicate-check state (WP-048),
    /// the source document's blob name, and recent audit history (WP-013/WP-052
    /// Part C) for one invoice. See <see cref="InvoiceDetailResponse"/>'s own doc
    /// comment for the one requested field this cannot currently return
    /// (per-field extraction confidence) and why.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InvoiceDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var invoiceResult = await _invoiceService.GetByIdAsync(id, cancellationToken);
        if (invoiceResult.IsFailure)
        {
            return NotFoundProblem(invoiceResult.Error.Code, invoiceResult.Error.Message);
        }

        return Ok(await BuildDetailResponseAsync(invoiceResult.Value, cancellationToken));
    }

    /// <summary>
    /// Returns only the workflow transitions the calling user may actually
    /// execute right now for this invoice (WP-054 task 1) - not the full
    /// transition graph with permission flags. See
    /// <see cref="IInvoiceWorkflowActionsService"/>'s own doc comment for why:
    /// this matches WP-018's "don't render a button that will always be
    /// rejected" approach and avoids duplicating role-policy logic client-side.
    /// </summary>
    [HttpGet("{id:guid}/available-actions")]
    [ProducesResponseType(typeof(IReadOnlyList<AvailableActionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAvailableActions(Guid id, CancellationToken cancellationToken)
    {
        var actionsResult = await _invoiceWorkflowActionsService.GetAvailableActionsAsync(id, cancellationToken);
        if (actionsResult.IsFailure)
        {
            return ErrorProblem(actionsResult.Error);
        }

        return Ok(actionsResult.Value.Select(AvailableActionResponse.FromDto).ToList());
    }

    /// <summary>
    /// Executes an invoice status change (WP-054 task 2) - a thin HTTP wrapper
    /// over the already-built, already-enforced (WP-053)
    /// <c>InvoiceService.UpdateAsync</c> path: no transition or role-gating rule
    /// is decided here. Fetches the invoice's current editable fields first and
    /// resubmits them unchanged alongside the new status, because
    /// <c>UpdateAsync</c>'s contract is a full field replace, not a partial
    /// patch - this endpoint's own request body only carries
    /// <see cref="UpdateInvoiceStatusRequest.TargetStatusCode"/> (plus optional
    /// notes), so every other field is round-tripped as-is. On success, an
    /// optional freeform note is recorded via the existing
    /// <see cref="IInvoiceService.AddNoteAsync"/> mechanism (WP-009) - not a new
    /// per-transition note type (task 4). <c>InvoiceService.UpdateAsync</c>
    /// already stages an <c>InvoiceStatusChanged</c> audit entry itself (WP-013)
    /// for the status change; this endpoint does not duplicate that (task 5).
    /// </summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(InvoiceDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateStatus(Guid id, UpdateInvoiceStatusRequest request, CancellationToken cancellationToken)
    {
        var currentResult = await _invoiceService.GetByIdAsync(id, cancellationToken);
        if (currentResult.IsFailure)
        {
            return ErrorProblem(currentResult.Error);
        }

        var current = currentResult.Value;

        var updateRequest = new UpdateInvoiceRequest(
            SupplierInvoiceNumber: current.SupplierInvoiceNumber,
            InvoiceDate: current.InvoiceDate,
            DueDate: current.DueDate,
            Currency: current.Currency,
            NetAmount: current.NetAmount,
            Vat: current.Vat,
            GrossTotal: current.GrossTotal,
            Status: request.TargetStatusCode);

        var updateResult = await _invoiceService.UpdateAsync(id, updateRequest, cancellationToken);
        if (updateResult.IsFailure)
        {
            return ErrorProblem(updateResult.Error);
        }

        if (!string.IsNullOrWhiteSpace(request.Notes))
        {
            var noteResult = await _invoiceService.AddNoteAsync(id, request.Notes, cancellationToken);
            if (noteResult.IsFailure)
            {
                // Same reasoning as every audit/side-effect failure path
                // elsewhere in this codebase: the status change itself already
                // succeeded and committed, so a failed note is a smaller
                // problem than hiding or reverting that success because of it.
                _logger.LogWarning(
                    "Failed to add note for invoice {InvoiceId} during status change: {ErrorCode} - {ErrorMessage}",
                    id, noteResult.Error.Code, noteResult.Error.Message);
            }
        }

        return Ok(await BuildDetailResponseAsync(updateResult.Value, cancellationToken));
    }

    /// <summary>
    /// Streams the invoice's source PDF document, proxied through this API - never
    /// a raw SAS URL handed back to the caller (WP-052 Part D's explicit
    /// instruction). Stages a <see cref="AuditActions.DocumentViewed"/> audit
    /// entry on success, via <see cref="IAuditService.LogAndSaveAsync"/> (not
    /// <see cref="IAuditService.LogAsync"/>: this is a read-only GET with no other
    /// change in the same request to commit the entry together with - see
    /// <see cref="IAuditService.LogAndSaveAsync"/>'s own doc comment).
    /// </summary>
    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var invoiceResult = await _invoiceService.GetByIdAsync(id, cancellationToken);
        if (invoiceResult.IsFailure)
        {
            return NotFoundProblem(invoiceResult.Error.Code, invoiceResult.Error.Message);
        }

        var blobName = invoiceResult.Value.SourceDocumentBlobName;
        if (string.IsNullOrWhiteSpace(blobName))
        {
            return NotFoundProblem("Invoice.NoSourceDocument", $"Invoice '{id}' has no associated source document.");
        }

        var downloadResult = await _blobStorageService.DownloadAsync(blobName, cancellationToken);
        if (downloadResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to download source document for invoice {InvoiceId} (blob {BlobName}): {ErrorCode} - {ErrorMessage}",
                id, blobName, downloadResult.Error.Code, downloadResult.Error.Message);
            return NotFoundProblem(downloadResult.Error.Code, downloadResult.Error.Message);
        }

        var auditResult = await _auditService.LogAndSaveAsync(
            new RecordAuditLogRequest(
                Action: AuditActions.DocumentViewed,
                EntityName: nameof(Invoice),
                EntityId: id,
                PreviousValue: null,
                NewValue: null),
            cancellationToken);

        if (auditResult.IsFailure)
        {
            // The document is still returned - a missing "viewed" audit entry is
            // a smaller problem than refusing a legitimate, already-authorized
            // document view because of it. Same reasoning as every other
            // audit-staging failure path in this codebase (see
            // InvoiceService.UpdateAsync/CreateAsync/DeleteAsync/AddNoteAsync).
            _logger.LogWarning(
                "Failed to record DocumentViewed audit entry for invoice {InvoiceId}: {ErrorCode} - {ErrorMessage}",
                id, auditResult.Error.Code, auditResult.Error.Message);
        }

        // Content type: not persisted per invoice (see PdfAttachmentDto/WP-007) -
        // every document this pipeline ingests is a PDF (WP-007's own extraction
        // is PDF-specific), so this is a safe, documented assumption rather than a
        // guess with unclear grounds.
        return File(downloadResult.Value, "application/pdf");
    }

    /// <summary>
    /// Returns every note recorded against an invoice, most recent first
    /// (WP-055) - a thin wrapper over the existing
    /// <see cref="IInvoiceService.GetNotesAsync"/>, itself over WP-009's
    /// <c>IInvoiceRepository.GetByIdWithNotesAsync</c>.
    /// </summary>
    [HttpGet("{id:guid}/notes")]
    [ProducesResponseType(typeof(IReadOnlyList<InvoiceNoteDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetNotes(Guid id, CancellationToken cancellationToken)
    {
        var notesResult = await _invoiceService.GetNotesAsync(id, cancellationToken);
        if (notesResult.IsFailure)
        {
            return ErrorProblem(notesResult.Error);
        }

        return Ok(notesResult.Value);
    }

    /// <summary>
    /// Adds a freeform note to an invoice (WP-055) - a thin wrapper over the
    /// existing <see cref="IInvoiceService.AddNoteAsync"/> (WP-009), which
    /// already performs the content validation (empty/over-length -&gt;
    /// <c>Invoice.InvalidNoteContent</c>, mapped to 400 by
    /// <see cref="ErrorProblem"/>'s default case) this endpoint relies on - no
    /// validation is duplicated here. Returns <c>201 Created</c> with the
    /// created note (including its server-assigned id and timestamp) and a
    /// <c>Location</c> header pointing at <see cref="GetNotes"/>, per the usual
    /// convention for a POST that creates a resource.
    /// </summary>
    [HttpPost("{id:guid}/notes")]
    [ProducesResponseType(typeof(InvoiceNoteDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CreateNote(Guid id, CreateInvoiceNoteRequest request, CancellationToken cancellationToken)
    {
        var noteResult = await _invoiceService.AddNoteAsync(id, request.Content, cancellationToken);
        if (noteResult.IsFailure)
        {
            return ErrorProblem(noteResult.Error);
        }

        return CreatedAtAction(nameof(GetNotes), new { id }, noteResult.Value);
    }

    /// <summary>
    /// Builds the WP-052 Part D <see cref="InvoiceDetailResponse"/> shape for an
    /// already-fetched invoice - extracted so <see cref="UpdateStatus"/> (WP-054)
    /// returns the identical shape <see cref="GetById"/> does, from one place,
    /// rather than duplicating the audit-history-lookup-plus-fallback logic.
    /// </summary>
    private async Task<InvoiceDetailResponse> BuildDetailResponseAsync(InvoiceDto invoice, CancellationToken cancellationToken)
    {
        // Recent audit entries. A failure here (e.g. an unconfigured/misbehaving
        // audit query) does not fail the whole request - the invoice itself was
        // found successfully, and an empty history list is a smaller problem than
        // refusing to return an invoice a caller is otherwise allowed to see.
        var auditResult = await _auditQueryService.SearchAsync(
            new AuditLogQueryParameters(EntityName: nameof(Invoice), EntityId: invoice.Id),
            cancellationToken);

        if (auditResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to load audit history for invoice {InvoiceId}: {ErrorCode} - {ErrorMessage}. Returning the invoice with an empty history instead of failing the request.",
                invoice.Id, auditResult.Error.Code, auditResult.Error.Message);
        }

        var recentAuditEntries = auditResult.IsSuccess ? auditResult.Value.Items : Array.Empty<AuditLogDto>();

        // WP-056: replaces the WP-052 Part D placeholder note now that the
        // ingestion pipeline actually persists this. Same "a failure here
        // doesn't fail the whole request" reasoning as audit history above -
        // the invoice was found successfully regardless.
        var extractedFieldsResult = await _invoiceService.GetExtractedFieldsAsync(invoice.Id, cancellationToken);

        if (extractedFieldsResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to load extracted fields for invoice {InvoiceId}: {ErrorCode} - {ErrorMessage}. Returning the invoice with an empty extracted-fields list instead of failing the request.",
                invoice.Id, extractedFieldsResult.Error.Code, extractedFieldsResult.Error.Message);
        }

        var extractedFields = extractedFieldsResult.IsSuccess ? extractedFieldsResult.Value : Array.Empty<InvoiceExtractedFieldDto>();

        return new InvoiceDetailResponse(invoice, recentAuditEntries, extractedFields);
    }

    private ObjectResult NotFoundProblem(string errorCode, string errorMessage) =>
        Problem(title: errorCode, detail: errorMessage, statusCode: StatusCodes.Status404NotFound);

    /// <summary>
    /// Builds a <see cref="ProblemDetails"/> response for a failed
    /// <see cref="Result"/> from the WP-054 endpoints, carrying the error's own
    /// code in a <c>code</c> extension member (task 2's explicit requirement) so
    /// the frontend can branch on it directly rather than parsing
    /// <see cref="ProblemDetails.Title"/>. The status code is derived from which
    /// service produced the error, not invented per message: <c>Invoice.NotFound</c>
    /// maps to 404; the two <c>Approval.*</c> codes
    /// <see cref="IApprovalAuthorizationService"/> can return (an explicit role
    /// mismatch, or fail-closed on a missing policy) map to 403 - this is the
    /// real code behind what WP-054's task wording calls
    /// "Workflow.RoleNotPermitted"; no such code is actually produced anywhere
    /// in this codebase, so it is not fabricated here (see
    /// docs/WP-054-Invoice-Workflow-Transition-Api-Decisions.md). Every other
    /// code - all of <see cref="IWorkflowValidationService"/>'s own failures
    /// (<c>Workflow.InvalidToStatus</c>, <c>Workflow.TransitionNotAllowed</c>,
    /// etc.) plus ordinary field-validation errors - maps to 400, matching
    /// task 2's own "400/403" pairing.
    /// </summary>
    private ObjectResult ErrorProblem(Error error)
    {
        var statusCode = error.Code switch
        {
            "Invoice.NotFound" => StatusCodes.Status404NotFound,
            "Approval.Unauthorized" => StatusCodes.Status403Forbidden,
            "Approval.PolicyNotConfigured" => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };

        var problemDetails = new ProblemDetails
        {
            Title = error.Code,
            Detail = error.Message,
            Status = statusCode,
        };
        problemDetails.Extensions["code"] = error.Code;

        return StatusCode(statusCode, problemDetails);
    }
}
