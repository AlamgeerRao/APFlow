using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace APFlow.Application.Features.Invoices;

/// <summary>
/// Default implementation of <see cref="IDuplicateDetectionService"/>. Depends only
/// on <see cref="IInvoiceRepository"/> (a plain, EF-Core-free interface), so this
/// class is fully unit-testable with a fake repository - no database, no EF Core
/// provider required.
///
/// <para>
/// Matching rule: per WP-010's task list, an invoice is flagged as a potential
/// duplicate of another invoice when ALL FOUR of the following match:
/// </para>
/// <list type="bullet">
///   <item><description><b>Supplier</b> - <see cref="Invoice.SupplierId"/> is equal.</description></item>
///   <item><description><b>Invoice Number</b> - <see cref="Invoice.SupplierInvoiceNumber"/> is equal,
///   compared trimmed and case-insensitively, since the same number may be extracted
///   with different casing/whitespace across separate WP-008 analysis runs.</description></item>
///   <item><description><b>Invoice Date</b> - <see cref="Invoice.InvoiceDate"/> is equal.</description></item>
///   <item><description><b>Gross Amount</b> - <see cref="Invoice.GrossTotal"/> is equal, using
///   exact decimal comparison. No fuzzy/tolerance matching is applied: no tolerance
///   was specified, and guessing one risks either hiding real duplicates or flagging
///   unrelated invoices.</description></item>
/// </list>
/// <para>
/// If either invoice being compared is missing any of the three nullable comparison
/// fields (Invoice Number, Invoice Date, Gross Amount), a meaningful comparison
/// cannot be made - two invoices that are both missing, say, an invoice number are
/// not thereby duplicates of each other - so that pair is skipped rather than risking
/// a false positive.
/// </para>
/// </summary>
public sealed class DuplicateDetectionService : IDuplicateDetectionService
{
    private const int RequiredMatchedFieldCount = 4;

    private readonly IInvoiceRepository _invoiceRepository;
    private readonly ILogger<DuplicateDetectionService> _logger;

    /// <summary>Creates a new <see cref="DuplicateDetectionService"/>.</summary>
    public DuplicateDetectionService(IInvoiceRepository invoiceRepository, ILogger<DuplicateDetectionService> logger)
    {
        _invoiceRepository = invoiceRepository;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<DuplicateCheckResult>> CheckAsync(Guid invoiceId, CancellationToken cancellationToken = default)
    {
        var candidate = await _invoiceRepository.GetByIdAsync(invoiceId, cancellationToken);
        if (candidate is null)
        {
            return Result.Failure<DuplicateCheckResult>(
                new Error("DuplicateDetection.InvoiceNotFound", $"Invoice '{invoiceId}' was not found."));
        }

        if (!HasComparableFields(candidate))
        {
            _logger.LogInformation(
                "Skipped duplicate check for invoice {InvoiceId}: one or more comparison fields " +
                "(SupplierInvoiceNumber, InvoiceDate, GrossTotal) is missing.",
                invoiceId);

            return Result.Success(new DuplicateCheckResult(invoiceId, false, Array.Empty<DuplicateMatch>()));
        }

        var allInvoices = await _invoiceRepository.GetAllAsync(cancellationToken);

        var matches = allInvoices
            .Where(other => other.Id != candidate.Id)
            .Select(other => TryMatch(candidate, other))
            .Where(match => match is not null)
            .Select(match => match!)
            .ToList();

        var result = new DuplicateCheckResult(invoiceId, matches.Count > 0, matches);

        LogOutcome(candidate, result);

        return Result.Success(result);
    }

    private void LogOutcome(Invoice candidate, DuplicateCheckResult result)
    {
        if (result.IsPotentialDuplicate)
        {
            _logger.LogWarning(
                "Potential duplicate detected for invoice {InvoiceId} (Supplier {SupplierId}, " +
                "Number {SupplierInvoiceNumber}, Date {InvoiceDate}, Gross {GrossTotal}): matches " +
                "{MatchCount} existing invoice(s): {MatchedInvoiceIds}.",
                candidate.Id,
                candidate.SupplierId,
                candidate.SupplierInvoiceNumber,
                candidate.InvoiceDate,
                candidate.GrossTotal,
                result.Matches.Count,
                string.Join(", ", result.Matches.Select(m => m.MatchedInvoiceId)));
        }
        else
        {
            _logger.LogInformation("No potential duplicates found for invoice {InvoiceId}.", candidate.Id);
        }
    }

    private static bool HasComparableFields(Invoice invoice) =>
        !string.IsNullOrWhiteSpace(invoice.SupplierInvoiceNumber)
        && invoice.InvoiceDate is not null
        && invoice.GrossTotal is not null;

    private static DuplicateMatch? TryMatch(Invoice candidate, Invoice other)
    {
        if (!HasComparableFields(other))
        {
            return null;
        }

        var matchedFields = new List<string>();

        if (candidate.SupplierId == other.SupplierId)
        {
            matchedFields.Add("Supplier");
        }

        if (string.Equals(candidate.SupplierInvoiceNumber?.Trim(), other.SupplierInvoiceNumber?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            matchedFields.Add("InvoiceNumber");
        }

        if (candidate.InvoiceDate == other.InvoiceDate)
        {
            matchedFields.Add("InvoiceDate");
        }

        if (candidate.GrossTotal == other.GrossTotal)
        {
            matchedFields.Add("GrossAmount");
        }

        if (matchedFields.Count != RequiredMatchedFieldCount)
        {
            return null;
        }

        var reason =
            $"Matches existing invoice {other.Id} on Supplier, Invoice Number " +
            $"('{other.SupplierInvoiceNumber}'), Invoice Date ({other.InvoiceDate}), " +
            $"and Gross Amount ({other.GrossTotal}).";

        return new DuplicateMatch(other.Id, other.SupplierInvoiceNumber, matchedFields, reason);
    }
}
