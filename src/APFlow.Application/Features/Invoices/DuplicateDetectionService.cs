using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace APFlow.Application.Features.Invoices;

/// <summary>
/// Default implementation of <see cref="IDuplicateDetectionService"/>. A pure
/// compute service (WP-048): depends only on <see cref="ILogger{TCategoryName}"/>
/// for diagnostics - no <see cref="APFlow.Application.Interfaces.IInvoiceRepository"/>,
/// no DbContext, no persistence dependency of any kind. Fully unit-testable with
/// plain in-memory <see cref="Invoice"/> instances - no fake repository required.
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
/// Invoice Date and Gross Amount are deliberately NOT part of this comparison - see
/// docs/WP-047-Duplicate-Matching-Reconciliation.md. No date-window or amount-based
/// fallback/OR-branch exists either.
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

    private readonly ILogger<DuplicateDetectionService> _logger;

    /// <summary>Creates a new <see cref="DuplicateDetectionService"/>.</summary>
    public DuplicateDetectionService(ILogger<DuplicateDetectionService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public DuplicateCheckResult Check(Invoice candidate, IReadOnlyList<Invoice> otherInvoices)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ArgumentNullException.ThrowIfNull(otherInvoices);

        if (!HasComparableFields(candidate))
        {
            _logger.LogInformation(
                "Skipped duplicate check for invoice {InvoiceId}: SupplierInvoiceNumber is missing.",
                candidate.Id);

            return new DuplicateCheckResult(candidate.Id, false, Array.Empty<DuplicateMatch>());
        }

        var matches = otherInvoices
            .Where(other => other.Id != candidate.Id)
            .Select(other => TryMatch(candidate, other))
            .Where(match => match is not null)
            .Select(match => match!)
            .ToList();

        var result = new DuplicateCheckResult(candidate.Id, matches.Count > 0, matches);

        LogOutcome(candidate, result);

        return result;
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
