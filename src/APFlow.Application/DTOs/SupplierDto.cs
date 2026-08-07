using APFlow.Domain.Common.Constants;

namespace APFlow.Application.DTOs;

/// <summary>Read shape for a supplier.</summary>
public sealed record SupplierDto(
    Guid Id,
    string Name,
    string? Code,
    string? Email,
    string? Phone,
    decimal? CreditLimit,
    int? PaymentTermsDays,
    string? AccountingReference,
    string Status,
    DateTimeOffset CreatedAtUtc);

/// <summary>
/// Request shape for creating or updating a supplier. <see cref="Status"/> defaults
/// to <see cref="SupplierStatusCodes.Active"/> when not supplied (e.g. a caller that
/// only sends <see cref="Name"/>), matching
/// <see cref="APFlow.Domain.Entities.Supplier.Status"/>'s own entity default.
/// </summary>
public sealed record SaveSupplierRequest(
    string Name,
    string? Code = null,
    string? Email = null,
    string? Phone = null,
    decimal? CreditLimit = null,
    int? PaymentTermsDays = null,
    string? AccountingReference = null,
    string Status = SupplierStatusCodes.Active);
