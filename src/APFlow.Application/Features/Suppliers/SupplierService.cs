using APFlow.Application.Common;
using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Common.Constants;
using APFlow.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace APFlow.Application.Features.Suppliers;

/// <summary>
/// Default implementation of <see cref="ISupplierService"/>. Fully unit-testable via
/// a fake <see cref="ISupplierRepository"/>/<see cref="ICurrentUserService"/> - the
/// latter added by WP-026 task 5's CreditLimit role check (see
/// <see cref="CallerMayChangeCreditLimit"/>).
/// </summary>
public sealed class SupplierService : ISupplierService
{
    private readonly ISupplierRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<SupplierService> _logger;

    /// <summary>Creates a new <see cref="SupplierService"/>.</summary>
    public SupplierService(ISupplierRepository repository, ICurrentUserService currentUserService, ILogger<SupplierService> logger)
    {
        _repository = repository;
        _currentUserService = currentUserService;
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
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return Result.Failure<SupplierDto>(validationError);
        }

        if (request.CreditLimit is not null && !CallerMayChangeCreditLimit())
        {
            return Result.Failure<SupplierDto>(CreditLimitForbiddenError());
        }

        var supplier = new Supplier
        {
            Name = request.Name,
            Code = request.Code,
            Email = request.Email,
            Phone = request.Phone,
            CreditLimit = request.CreditLimit,
            PaymentTermsDays = request.PaymentTermsDays,
            AccountingReference = request.AccountingReference,
            Status = request.Status,
        };

        await _repository.AddAsync(supplier, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created supplier {SupplierId} ({SupplierName}).", supplier.Id, supplier.Name);

        return Result.Success(ToDto(supplier));
    }

    /// <inheritdoc />
    public async Task<Result<SupplierDto>> UpdateAsync(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = Validate(request);
        if (validationError is not null)
        {
            return Result.Failure<SupplierDto>(validationError);
        }

        var supplier = await _repository.GetByIdAsync(id, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure<SupplierDto>(new Error("Supplier.NotFound", $"Supplier '{id}' was not found."));
        }

        if (request.CreditLimit != supplier.CreditLimit && !CallerMayChangeCreditLimit())
        {
            return Result.Failure<SupplierDto>(CreditLimitForbiddenError());
        }

        supplier.Name = request.Name;
        supplier.Code = request.Code;
        supplier.Email = request.Email;
        supplier.Phone = request.Phone;
        supplier.CreditLimit = request.CreditLimit;
        supplier.PaymentTermsDays = request.PaymentTermsDays;
        supplier.AccountingReference = request.AccountingReference;
        supplier.Status = request.Status;
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
    /// WP-026 task 5: any authenticated user with base access may update every
    /// supplier field except CreditLimit - only a caller holding
    /// <see cref="Roles.FinanceManager"/> may set or change it. Deliberately a direct
    /// role check against <see cref="ICurrentUserService"/>, not the
    /// WorkflowTransition/ApprovalPolicy machinery (that machinery gates invoice
    /// status transitions specifically - this is an unrelated, simpler field-level
    /// permission with no workflow/policy row of its own).
    /// </summary>
    private bool CallerMayChangeCreditLimit() => _currentUserService.IsInRole(Roles.FinanceManager);

    private static Error CreditLimitForbiddenError() => new(
        "Supplier.CreditLimitForbidden",
        $"Changing a supplier's credit limit requires the '{Roles.FinanceManager}' role.");

    /// <summary>
    /// Validates every field of a create/update request against FieldLimits (and, for
    /// CreditLimit/PaymentTermsDays/Status, basic domain sanity) before anything
    /// touches the repository. Mirrors SupplierConfiguration's constraints, same
    /// pattern as the original name-only validation.
    /// </summary>
    private static Error? Validate(SaveSupplierRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return new Error("Supplier.InvalidName", "Supplier name must not be empty.");
        }

        if (request.Name.Length > FieldLimits.SupplierName)
        {
            return new Error("Supplier.InvalidName", $"Supplier name must not exceed {FieldLimits.SupplierName} characters.");
        }

        if (request.Code is { Length: > 0 } code && code.Length > FieldLimits.SupplierCode)
        {
            return new Error("Supplier.InvalidCode", $"Supplier code must not exceed {FieldLimits.SupplierCode} characters.");
        }

        if (request.Email is { Length: > 0 } email && email.Length > FieldLimits.SupplierEmail)
        {
            return new Error("Supplier.InvalidEmail", $"Supplier email must not exceed {FieldLimits.SupplierEmail} characters.");
        }

        if (request.Phone is { Length: > 0 } phone && phone.Length > FieldLimits.SupplierPhone)
        {
            return new Error("Supplier.InvalidPhone", $"Supplier phone must not exceed {FieldLimits.SupplierPhone} characters.");
        }

        if (request.CreditLimit is < 0)
        {
            return new Error("Supplier.InvalidCreditLimit", "Supplier credit limit must not be negative.");
        }

        if (request.PaymentTermsDays is < 0)
        {
            return new Error("Supplier.InvalidPaymentTerms", "Supplier payment terms (days) must not be negative.");
        }

        if (request.AccountingReference is { Length: > 0 } accountingReference
            && accountingReference.Length > FieldLimits.SupplierAccountingReference)
        {
            return new Error(
                "Supplier.InvalidAccountingReference",
                $"Supplier accounting reference must not exceed {FieldLimits.SupplierAccountingReference} characters.");
        }

        if (request.Status != SupplierStatusCodes.Active && request.Status != SupplierStatusCodes.Inactive)
        {
            return new Error(
                "Supplier.InvalidStatus",
                $"Supplier status must be '{SupplierStatusCodes.Active}' or '{SupplierStatusCodes.Inactive}'.");
        }

        return null;
    }

    private static SupplierDto ToDto(Supplier supplier) => new(
        supplier.Id,
        supplier.Name,
        supplier.Code,
        supplier.Email,
        supplier.Phone,
        supplier.CreditLimit,
        supplier.PaymentTermsDays,
        supplier.AccountingReference,
        supplier.Status,
        supplier.CreatedAtUtc);
}
