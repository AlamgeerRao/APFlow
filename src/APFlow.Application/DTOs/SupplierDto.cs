namespace APFlow.Application.DTOs;

/// <summary>Read shape for a supplier.</summary>
public sealed record SupplierDto(Guid Id, string Name, DateTimeOffset CreatedAtUtc);

/// <summary>Request shape for creating or updating a supplier.</summary>
public sealed record SaveSupplierRequest(string Name);
