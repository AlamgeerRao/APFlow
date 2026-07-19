using APFlow.Application.DTOs;
using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>CRUD orchestration for suppliers.</summary>
public interface ISupplierService
{
    /// <summary>Returns the supplier with the given id.</summary>
    Task<Result<SupplierDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Returns every supplier visible to the current tenant.</summary>
    Task<Result<IReadOnlyList<SupplierDto>>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>Creates a new supplier.</summary>
    Task<Result<SupplierDto>> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken = default);

    /// <summary>Updates an existing supplier.</summary>
    Task<Result<SupplierDto>> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken = default);

    /// <summary>Soft-deletes a supplier.</summary>
    Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
