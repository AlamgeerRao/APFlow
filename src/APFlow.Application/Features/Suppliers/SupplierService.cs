using APFlow.Application.Common;
using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace APFlow.Application.Features.Suppliers;

/// <summary>Default implementation of <see cref="ISupplierService"/>. Fully unit-testable via a fake <see cref="ISupplierRepository"/>.</summary>
public sealed class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _repository;
    private readonly ILogger<SupplierService> _logger;

    /// <summary>Creates a new <see cref="SupplierService"/>.</summary>
    public SupplierService(ISupplierRepository repository, ILogger<SupplierService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<SupplierDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplier = await _repository.GetByIdAsync(id, cancellationToken);

        return supplier is null
            ? Result.Failure<SupplierDto>(new Error("Supplier.NotFound", $"Supplier '{id}' was not found."))
            : Result.Success(ToDto(supplier));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<SupplierDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var suppliers = await _repository.GetAllAsync(cancellationToken);
        return Result.Success<IReadOnlyList<SupplierDto>>(suppliers.Select(ToDto).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<SupplierDto>> CreateAsync(SaveSupplierRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateName(request.Name);
        if (validationError is not null)
        {
            return Result.Failure<SupplierDto>(validationError);
        }

        var supplier = new Supplier { Name = request.Name };

        await _repository.AddAsync(supplier, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created supplier {SupplierId} ({SupplierName}).", supplier.Id, supplier.Name);

        return Result.Success(ToDto(supplier));
    }

    /// <inheritdoc />
    public async Task<Result<SupplierDto>> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateName(request.Name);
        if (validationError is not null)
        {
            return Result.Failure<SupplierDto>(validationError);
        }

        var supplier = await _repository.GetByIdAsync(id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure<SupplierDto>(new Error("Supplier.NotFound", $"Supplier '{id}' was not found."));
        }

        supplier.Name = request.Name;
        _repository.Update(supplier);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated supplier {SupplierId}.", id);

        return Result.Success(ToDto(supplier));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var supplier = await _repository.GetByIdAsync(id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure(new Error("Supplier.NotFound", $"Supplier '{id}' was not found."));
        }

        _repository.Remove(supplier);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted (soft) supplier {SupplierId}.", id);

        return Result.Success();
    }

    /// <summary>
    /// Validates the supplier name against FieldLimits before anything touches the
    /// repository. Mirrors SupplierConfiguration's constraint.
    /// </summary>
    private static Error? ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new Error("Supplier.InvalidName", "Supplier name must not be empty.");
        }

        if (name.Length > FieldLimits.SupplierName)
        {
            return new Error("Supplier.InvalidName", $"Supplier name must not exceed {FieldLimits.SupplierName} characters.");
        }

        return null;
    }

    private static SupplierDto ToDto(Supplier supplier) => new(supplier.Id, supplier.Name, supplier.CreatedAtUtc);
}
