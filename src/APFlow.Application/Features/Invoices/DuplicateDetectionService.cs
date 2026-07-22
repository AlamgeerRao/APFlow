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
/// Matching rule (WP-047, correcting WP-010's original four-field rule per the
/// Product Owner's confirmed client requirement - see
/// docs/WP-047-Duplicate-Matching-Reconciliation.md): an invoice is flagged as a
/// potential duplicate of another invoice when BOTH of the following match:
/// </para>
/// <list type="bullet">
///   <item><description><b>Supplier</b> - <see cref="Invoice.SupplierId"/> is equal.</description></item>
///   <item><description><b>Invoice Number</b> - <see cref="Invoice.SupplierInvoiceNumber"/> is equal,
///   compared trimmed and case-insensitively, since the same number may be extracted
///   with different casing/whitespace across separate WP-008 analysis runs.</description></item>
/// </list>
/// <para>
/// Invoice Date and Gross Amount are deliberately NOT part of this comparison.
/// WP-010's original four-field rule (Supplier + Invoice Number + Invoice Date +
/// Gross Amount, all required) has been superseded - the confirmed rule is Supplier
/// + Invoice Number alone. No date-window or amount-based fallback/OR-branch exists
/// either - explicitly out of scope for WP-047, and intentionally absent here.
/// </para>
/// <para>
/// If either invoice being compared is missing Invoice Number, a meaningful
/// comparison cannot be made - two invoices both missing an invoice number are not
/// thereby duplicates of each other - so that pair is skipped rather than risking a
/// false positive.
/// </para>
/// </summary>
public sealed class DuplicateDetectionService : IDuplicateDetectionService
{
    private const int RequiredMatchedFieldCount = 2;

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
                "Skipped duplicate check for invoice {InvoiceId}: SupplierInvoiceNumber is missing.",
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
                "Number {SupplierInvoiceNumber}): matches {MatchCount} existing invoice(s): {MatchedInvoiceIds}.",
                candidate.Id,
                candidate.SupplierId,
                candidate.SupplierInvoiceNumber,
                result.Matches.Count,
                string.Join(", ", result.Matches.Select(m => m.MatchedInvoiceId)));
        }
        else
        {
            _logger.LogInformation("No potential duplicates found for invoice {InvoiceId}.", candidate.Id);
        }
    }

    private static bool HasComparableFields(Invoice invoice) =>
        !string.IsNullOrWhiteSpace(invoice.SupplierInvoiceNumber);

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

        if (matchedFields.Count != RequiredMatchedFieldCount)
        {
            return null;
        }

        var reason = $"Matches existing invoice {other.Id} on Supplier and Invoice Number ('{other.SupplierInvoiceNumber}').";

        return new DuplicateMatch(other.Id, other.SupplierInvoiceNumber, matchedFields, reason);
    }
}
