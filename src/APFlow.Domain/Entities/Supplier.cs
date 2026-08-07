using APFlow.Domain.Common.Constants;

namespace APFlow.Domain.Entities;

/// <summary>
/// A supplier/vendor that issues invoices to the tenant. Originally deliberately
/// minimal (only <see cref="Name"/>) - see WP-009/WP-012's own scope notes for why.
/// WP-026 (Sprint 2 - Supplier Management) is that "real requirement": adds a code,
/// contact details, credit limit, payment terms, an accounting reference (for the
/// eventual Sage 50 export, WP-042), and a status, all still address/tax-id/
/// bank-detail-free since nothing has driven those yet.
/// </summary>
public sealed class Supplier : TenantEntity
{
    /// <summary>The supplier's name, as it should appear to AP Flow users (may differ from the exact string extracted from any one invoice).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Optional short internal reference code for this supplier (distinct from <see cref="AccountingReference"/>, which is specifically for the Sage 50 export match).</summary>
    public string? Code { get; set; }

    /// <summary>Optional contact email for this supplier.</summary>
    public string? Email { get; set; }

    /// <summary>Optional contact phone number for this supplier.</summary>
    public string? Phone { get; set; }

    /// <summary>
    /// Optional credit limit for this supplier, in the tenant's base currency. Not
    /// every supplier has one (e.g. GB Skips' own skip-hire suppliers are typically
    /// invoiced per job, not extended credit) - nullable, no default, no assumed
    /// currency conversion.
    /// </summary>
    public decimal? CreditLimit { get; set; }

    /// <summary>
    /// Optional standard payment terms for this supplier, expressed as "net days"
    /// (e.g. 30 for "net 30") - a plain integer, not a lookup against any terms
    /// catalogue. WP-026 decision: no existing catalogue/convention for payment
    /// terms exists in docs/AI/06_Domain_Reference_Data.md (that document covers
    /// only the role and invoice-status catalogues), so this is the minimal,
    /// reasoned representation rather than an invented enum of named terms
    /// ("Net 30"/"Due on Receipt"/etc.) - see docs/WP-026-Supplier-Management-Report.md.
    /// </summary>
    public int? PaymentTermsDays { get; set; }

    /// <summary>
    /// Optional free-form reference used to match this supplier to its
    /// corresponding record in Sage 50 for the eventual accounting export
    /// (WP-042). Deliberately a plain string, not a structured/validated code -
    /// Sage 50's own reference format is out of scope here and shouldn't be
    /// guessed at ahead of WP-042's real integration requirements.
    /// </summary>
    public string? AccountingReference { get; set; }

    /// <summary>
    /// Whether the supplier is currently active. WP-026 decision: no existing
    /// supplier-status catalogue exists in docs/AI/06_Domain_Reference_Data.md (only
    /// role and invoice-status catalogues are documented there), so this uses the
    /// minimal reasoned default - <see cref="SupplierStatusCodes.Active"/>/
    /// <see cref="SupplierStatusCodes.Inactive"/> - rather than an invented richer
    /// state machine. A plain string (not an enum), matching
    /// <see cref="Invoice.Status"/>'s own reasoning: keeps the representation
    /// consistent across the codebase and open to becoming tenant-configurable
    /// later without a schema change, even though nothing currently makes supplier
    /// status tenant-specific the way invoice workflow status is.
    /// </summary>
    public string Status { get; set; } = SupplierStatusCodes.Active;

    /// <summary>Invoices received from this supplier.</summary>
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
