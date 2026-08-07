using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace APFlow.Api.Controllers;

/// <summary>
/// Supplier CRUD endpoints (WP-026, Sprint 2). No HTTP surface existed for
/// suppliers before this WP - <see cref="ISupplierService"/> (WP-009) was only ever
/// called internally (duplicate detection, supplier-folder queries). A thin wrapper
/// over <see cref="ISupplierService"/>, no business logic of its own - same
/// convention as every other controller in this project (see
/// <c>InvoicesController</c>'s own doc comment). Binds directly to
/// <see cref="SaveSupplierRequest"/>/<see cref="SupplierDto"/> for the request/response
/// body rather than introducing separate wire-format contract types, since those
/// Application-layer DTOs are already exactly the shape a create/update/read caller
/// needs - no field renaming or reshaping happens in this controller. Tenant
/// isolation is enforced entirely by the underlying <c>AppDbContext</c> query filter
/// (via <see cref="ISupplierRepository"/>), not by this controller.
/// </summary>
[ApiController]
[Route("api/suppliers")]
public sealed class SuppliersController : ControllerBase
{
    private readonly ISupplierService _supplierService;

    /// <summary>Creates a new <see cref="SuppliersController"/>.</summary>
    public SuppliersController(ISupplierService supplierService)
    {
        _supplierService = supplierService;
    }

    /// <summary>Returns every supplier visible to the current tenant.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<SupplierDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _supplierService.GetAllAsync(cancellationToken);
        if (result.IsFailure)
        {
            return ErrorProblem(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>Returns the supplier with the given id.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _supplierService.GetByIdAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorProblem(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>
    /// Creates a new supplier. Returns <c>201 Created</c> with the created supplier
    /// and a <c>Location</c> header pointing at <see cref="GetById"/>, the usual
    /// convention for a POST that creates a resource (same as
    /// <c>InvoicesController.CreateNote</c>).
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        var result = await _supplierService.CreateAsync(request, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorProblem(result.Error);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Value.Id }, result.Value);
    }

    /// <summary>Updates an existing supplier (full field replace, matching <see cref="ISupplierService.UpdateAsync"/>'s own contract).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(SupplierDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, SaveSupplierRequest request, CancellationToken cancellationToken)
    {
        var result = await _supplierService.UpdateAsync(id, request, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorProblem(result.Error);
        }

        return Ok(result.Value);
    }

    /// <summary>Soft-deletes a supplier (<see cref="ISupplierService.DeleteAsync"/>, itself backed by <c>AuditEntity.IsDeleted</c>, not a hard delete).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _supplierService.DeleteAsync(id, cancellationToken);
        if (result.IsFailure)
        {
            return ErrorProblem(result.Error);
        }

        return NoContent();
    }

    /// <summary>
    /// Builds a <see cref="ProblemDetails"/> response for a failed <see cref="Result"/>,
    /// carrying the error's own code in a <c>code</c> extension member - same
    /// convention as every other controller in this project.
    /// <c>Supplier.NotFound</c> maps to 404; <c>Supplier.CreditLimitForbidden</c>
    /// (WP-026 task 5 - a non-FINANCE_MANAGER caller tried to change CreditLimit)
    /// maps to 403; every other code produced by <see cref="ISupplierService"/>
    /// (the <c>Supplier.Invalid*</c> validation errors) maps to 400.
    /// </summary>
    private ObjectResult ErrorProblem(Error error)
    {
        var statusCode = error.Code switch
        {
            "Supplier.NotFound" => StatusCodes.Status404NotFound,
            "Supplier.CreditLimitForbidden" => StatusCodes.Status403Forbidden,
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
