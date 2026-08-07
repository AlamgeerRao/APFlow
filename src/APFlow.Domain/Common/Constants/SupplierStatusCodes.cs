namespace APFlow.Domain.Common.Constants;

/// <summary>
/// Named constants for <see cref="APFlow.Domain.Entities.Supplier.Status"/> (WP-026).
/// docs/AI/06_Domain_Reference_Data.md has no supplier-status catalogue (it only
/// documents the role and invoice-status catalogues) - per that document's own "do
/// not invent, escalate instead" rule this is deliberately the minimal reasoned
/// default: a plain Active/Inactive flag, not an elaborate state machine. A string,
/// not an enum, for the same reason <c>Invoice.Status</c> is a string rather than
/// <c>InvoiceStatusCodes</c> itself - see that class's own doc comment - even though
/// nothing currently makes supplier status tenant-configurable.
/// </summary>
public static class SupplierStatusCodes
{
    /// <summary>The supplier is currently active and may receive/process invoices normally.</summary>
    public const string Active = "ACTIVE";

    /// <summary>The supplier is inactive - retained for historical/audit purposes, not currently in use.</summary>
    public const string Inactive = "INACTIVE";
}
