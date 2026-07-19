using APFlow.Domain.Enums;

namespace APFlow.Application.DTOs;

/// <summary>Read shape for an invoice.</summary>
public sealed record InvoiceDto(
    Guid Id,
    Guid SupplierId,
    string? SupplierName,
    string? SupplierInvoiceNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    string? Currency,
    decimal? NetAmount,
    decimal? Vat,
    decimal? GrossTotal,
    InvoiceStatus Status,
    string? SourceEmailMessageId,
    DateTimeOffset CreatedAtUtc);

/// <summary>Request shape for creating an invoice. No Id, no Status (always starts at InvoiceStatus.Received), no audit fields - those are owned by the entity/AppDbContext.</summary>
public sealed record CreateInvoiceRequest(
    Guid SupplierId,
    string? SupplierInvoiceNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    string? Currency,
    decimal? NetAmount,
    decimal? Vat,
    decimal? GrossTotal,
    string? SourceEmailMessageId);

/// <summary>
/// Request shape for updating an invoice's editable fields, including
/// <see cref="Status"/>. No transition validation is performed - "Approval
/// workflow" is explicit WP-009 out-of-scope, so any status can be set to any
/// other status here. A future work package is responsible for enforcing which
/// transitions are actually valid.
/// </summary>
public sealed record UpdateInvoiceRequest(
    string? SupplierInvoiceNumber,
    DateOnly? InvoiceDate,
    DateOnly? DueDate,
    string? Currency,
    decimal? NetAmount,
    decimal? Vat,
    decimal? GrossTotal,
    InvoiceStatus Status);
