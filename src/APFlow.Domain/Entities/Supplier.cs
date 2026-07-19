namespace APFlow.Domain.Entities;

/// <summary>
/// A supplier/vendor that issues invoices to the tenant. Deliberately minimal -
/// only <see cref="Name"/>. Address, tax identification numbers, bank details, and
/// similar fields are not included: nothing in WP-009's scope or WP-008's extracted
/// fields calls for them, and guessing at that shape now risks a schema that doesn't
/// match whatever a future work package actually needs. Add fields when a real
/// requirement drives them, not speculatively.
/// </summary>
public sealed class Supplier : TenantEntity
{
    /// <summary>The supplier's name, as it should appear to AP Flow users (may differ from the exact string extracted from any one invoice).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Invoices received from this supplier.</summary>
    public ICollection<Invoice> Invoices { get; set; } = new List<Invoice>();
}
