using APFlow.Api.Contracts;
using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace APFlow.Api.Controllers;

/// <summary>
/// Invoice read/document endpoints (WP-052 Part D). The solution-wide fallback
/// authorization policy (see <c>AuthorizationExtensions.AddApiAuthorization</c>)
/// already requires an authenticated caller for every action here - no
/// <c>[AllowAnonymous]</c> anywhere in this controller. Tenant isolation is
/// enforced entirely by the underlying services this controller composes
/// (<see cref="IInvoiceService"/>'s repository query filter,
/// <see cref="IBlobStorageService"/>'s tenant-prefixed blob names) - this
/// controller adds no tenant-checking logic of its own, matching
/// "APFlow.Api ... contains no business logic" (Solution Structure §1/§5).
/// </summary>
[ApiController]
[Route("api/invoices")]
public sealed class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _invoiceService;
    private readonly IAuditQueryService _auditQueryService;
    private readonly IAuditService _auditService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<InvoicesController> _logger;

    /// <summary>Creates a new <see cref="InvoicesController"/>.</summary>
    public InvoicesController(
        IInvoiceService invoiceService,
        IAuditQueryService auditQueryService,
        IAuditService auditService,
        IBlobStorageService blobStorageService,
        ILogger<InvoicesController> logger)
    {
        _invoiceService = invoiceService;
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

        // Recent audit entries. A failure here (e.g. an unconfigured/misbehaving
        // audit query) does not fail the whole request - the invoice itself was
        // found successfully, and an empty history list is a smaller problem than
        // refusing to return an invoice a caller is otherwise allowed to see.
        var auditResult = await _auditQueryService.SearchAsync(
            new AuditLogQueryParameters(EntityName: nameof(Invoice), EntityId: id),
            cancellationToken);

        if (auditResult.IsFailure)
        {
            _logger.LogWarning(
                "Failed to load audit history for invoice {InvoiceId}: {ErrorCode} - {ErrorMessage}. Returning the invoice with an empty history instead of failing the request.",
                id, auditResult.Error.Code, auditResult.Error.Message);
        }

        var recentAuditEntries = auditResult.IsSuccess ? auditResult.Value.Items : Array.Empty<AuditLogDto>();

        return Ok(new InvoiceDetailResponse(
            invoiceResult.Value,
            recentAuditEntries,
            ExtractionConfidenceNote:
                "Per-field extraction confidence is not currently persisted - see docs/WP-052-Pipeline-And-Api-Hardening-Decisions.md."));
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

    private ObjectResult NotFoundProblem(string errorCode, string errorMessage) =>
        Problem(title: errorCode, detail: errorMessage, statusCode: StatusCodes.Status404NotFound);
}
