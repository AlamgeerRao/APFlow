using APFlow.Application.Common;
using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace APFlow.Application.Features.Invoices;

/// <summary>
/// Default implementation of <see cref="IInvoiceService"/>. Depends only on
/// <see cref="IInvoiceRepository"/> and <see cref="ISupplierRepository"/> (both
/// plain, EF-Core-free interfaces), so this class is fully unit-testable with fake
/// repositories - no database, no EF Core provider required.
/// </summary>
public sealed class InvoiceService : IInvoiceService
{
    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ISupplierRepository _supplierRepository;
    private readonly ILogger<InvoiceService> _logger;

    /// <summary>Creates a new <see cref="InvoiceService"/>.</summary>
    public InvoiceService(IInvoiceRepository invoiceRepository, ISupplierRepository supplierRepository, ILogger<InvoiceService> logger)
    {
        _invoiceRepository = invoiceRepository;
        _supplierRepository = supplierRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<InvoiceDto>> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(id, cancellationToken);

        return invoice is null
            ? Result.Failure<InvoiceDto>(new Error("Invoice.NotFound", $"Invoice '{id}' was not found."))
            : Result.Success(ToDto(invoice));
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<InvoiceDto>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var invoices = await _invoiceRepository.GetAllAsync(cancellationToken);
        return Result.Success<IReadOnlyList<InvoiceDto>>(invoices.Select(ToDto).ToList());
    }

    /// <inheritdoc />
    public async Task<Result<InvoiceDto>> CreateAsync(CreateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateFields(request.SupplierInvoiceNumber, request.Currency);
        if (validationError is not null)
        {
            return Result.Failure<InvoiceDto>(validationError);
        }

        var supplier = await _supplierRepository.GetByIdAsync(request.SupplierId, cancellationToken);
        if (supplier is null)
        {
            return Result.Failure<InvoiceDto>(
                new Error("Invoice.SupplierNotFound", $"Supplier '{request.SupplierId}' was not found."));
        }

        var invoice = new Invoice
        {
            SupplierId = request.SupplierId,
            SupplierInvoiceNumber = request.SupplierInvoiceNumber,
            InvoiceDate = request.InvoiceDate,
            DueDate = request.DueDate,
            Currency = request.Currency,
            NetAmount = request.NetAmount,
            Vat = request.Vat,
            GrossTotal = request.GrossTotal,
            SourceEmailMessageId = request.SourceEmailMessageId,
            SourceDocumentBlobName = request.SourceDocumentBlobName,
        };

        await _invoiceRepository.AddAsync(invoice, cancellationToken);
        await _invoiceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Created invoice {InvoiceId} for supplier {SupplierId}.", invoice.Id, invoice.SupplierId);

        invoice.Supplier = supplier;
        return Result.Success(ToDto(invoice));
    }

    /// <inheritdoc />
    public async Task<Result<InvoiceDto>> UpdateAsync(Guid id, UpdateInvoiceRequest request, CancellationToken cancellationToken = default)
    {
        var validationError = ValidateFields(request.SupplierInvoiceNumber, request.Currency);
        if (validationError is not null)
        {
            return Result.Failure<InvoiceDto>(validationError);
        }

        var invoice = await _invoiceRepository.GetByIdAsync(id, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure<InvoiceDto>(new Error("Invoice.NotFound", $"Invoice '{id}' was not found."));
        }

        invoice.SupplierInvoiceNumber = request.SupplierInvoiceNumber;
        invoice.InvoiceDate = request.InvoiceDate;
        invoice.DueDate = request.DueDate;
        invoice.Currency = request.Currency;
        invoice.NetAmount = request.NetAmount;
        invoice.Vat = request.Vat;
        invoice.GrossTotal = request.GrossTotal;
        invoice.Status = request.Status;

        _invoiceRepository.Update(invoice);
        await _invoiceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Updated invoice {InvoiceId}. Status={Status}.", invoice.Id, invoice.Status);

        return Result.Success(ToDto(invoice));
    }

    /// <inheritdoc />
    public async Task<Result> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var invoice = await _invoiceRepository.GetByIdAsync(id, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure(new Error("Invoice.NotFound", $"Invoice '{id}' was not found."));
        }

        _invoiceRepository.Remove(invoice);
        await _invoiceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Deleted (soft) invoice {InvoiceId}.", id);

        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<Result> AddNoteAsync(Guid invoiceId, string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return Result.Failure(new Error("Invoice.InvalidNoteContent", "Note content must not be empty."));
        }

        if (content.Length > FieldLimits.InvoiceNoteContent)
        {
            return Result.Failure(new Error(
                "Invoice.InvalidNoteContent",
                $"Note content must not exceed {FieldLimits.InvoiceNoteContent} characters."));
        }

        var invoice = await _invoiceRepository.GetByIdWithNotesAsync(invoiceId, cancellationToken);
        if (invoice is null)
        {
            return Result.Failure(new Error("Invoice.NotFound", $"Invoice '{invoiceId}' was not found."));
        }

        invoice.Notes.Add(new InvoiceNote
        {
            InvoiceId = invoiceId,
            Content = content,
        });

        _invoiceRepository.Update(invoice);
        await _invoiceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Added note to invoice {InvoiceId}.", invoiceId);

        return Result.Success();
    }

    /// <summary>
    /// Validates field lengths against <see cref="FieldLimits"/> before anything
    /// touches the repository, so a value that would fail at the database as an
    /// opaque DbUpdateException instead comes back as a clean Result.Failure with a
    /// specific error code. Mirrors InvoiceConfiguration's constraints - see
    /// FieldLimits' doc comment for why these are duplicated rather than shared.
    /// </summary>
    private static Error? ValidateFields(string? supplierInvoiceNumber, string? currency)
    {
        if (supplierInvoiceNumber is { Length: > FieldLimits.InvoiceSupplierInvoiceNumber })
        {
            return new Error(
                "Invoice.InvalidSupplierInvoiceNumber",
                $"Supplier invoice number must not exceed {FieldLimits.InvoiceSupplierInvoiceNumber} characters.");
        }

        if (currency is { Length: > 0 } && currency.Length != FieldLimits.InvoiceCurrency)
        {
            return new Error(
                "Invoice.InvalidCurrency",
                $"Currency must be a {FieldLimits.InvoiceCurrency}-character ISO 4217 code (e.g. \"GBP\").");
        }

        return null;
    }

    private static InvoiceDto ToDto(Invoice invoice) => new(
        Id: invoice.Id,
        SupplierId: invoice.SupplierId,
        SupplierName: invoice.Supplier?.Name,
        SupplierInvoiceNumber: invoice.SupplierInvoiceNumber,
        InvoiceDate: invoice.InvoiceDate,
        DueDate: invoice.DueDate,
        Currency: invoice.Currency,
        NetAmount: invoice.NetAmount,
        Vat: invoice.Vat,
        GrossTotal: invoice.GrossTotal,
        Status: invoice.Status,
        SourceEmailMessageId: invoice.SourceEmailMessageId,
        SourceDocumentBlobName: invoice.SourceDocumentBlobName,
        IsPotentialDuplicate: invoice.IsPotentialDuplicate,
        DuplicateCheckReason: invoice.DuplicateCheckReason,
        CreatedAtUtc: invoice.CreatedAtUtc);
}
