using APFlow.Application.Common;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;

namespace APFlow.Application.Features.Invoices;

/// <summary>
/// Shared field-length/format validation for an invoice's editable fields, used by
/// both <see cref="InvoiceService"/> (its own create/update paths) and
/// <see cref="InvoiceProcessingService"/> (WP-049: which constructs and stages an
/// <see cref="APFlow.Domain.Entities.Invoice"/> directly, bypassing
/// <see cref="IInvoiceService.CreateAsync"/>, so it cannot rely on that method's
/// own validation). Extracted here (rather than duplicated in both places) so a
/// value that would fail at the database as an opaque DbUpdateException instead
/// comes back as a clean <see cref="Error"/> with a specific code, consistently,
/// wherever an invoice is created or updated. Mirrors <c>InvoiceConfiguration</c>'s
/// constraints - see <see cref="FieldLimits"/>'s doc comment for why these are
/// duplicated from the EF configuration rather than shared with it.
/// </summary>
internal static class InvoiceFieldValidation
{
    /// <summary>Returns an <see cref="Error"/> if either field is invalid, or null if both are acceptable.</summary>
    public static Error? Validate(string? supplierInvoiceNumber, string? currency)
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
}
