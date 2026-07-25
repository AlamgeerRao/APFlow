using APFlow.Application.DTOs;

namespace APFlow.Api.Contracts;

/// <summary>
/// Response shape for one entry of <c>GET /api/invoices/{id}/available-actions</c>
/// (WP-054 task 1). Composes <see cref="AvailableActionDto"/> as-is, same
/// "reuse the existing DTO's field names" reasoning as
/// <see cref="InvoiceDetailResponse"/>. Serializes as
/// <c>{ "targetStatusCode": "...", "targetStatusLabel": "..." }</c> under ASP.NET
/// Core's default camelCase policy for controller-based APIs.
/// </summary>
public sealed record AvailableActionResponse(string TargetStatusCode, string TargetStatusLabel)
{
    /// <summary>Creates a response entry from the Application-layer DTO.</summary>
    public static AvailableActionResponse FromDto(AvailableActionDto dto) =>
        new(dto.TargetStatusCode, dto.TargetStatusLabel);
}
