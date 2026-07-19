namespace APFlow.Application.Common;

/// <summary>
/// Field length limits mirroring the EF Core configurations in
/// APFlow.Infrastructure.Persistence.Configurations. Application cannot reference
/// Infrastructure (Solution Structure §2), so these are intentionally duplicated,
/// not shared via a common reference - if a database column's max length changes,
/// this file must be updated to match, or validation here will silently accept
/// values the database will still reject with a raw DbUpdateException.
/// </summary>
internal static class FieldLimits
{
    /// <summary>Matches SupplierConfiguration's Name column.</summary>
    public const int SupplierName = 256;

    /// <summary>Matches InvoiceConfiguration's SupplierInvoiceNumber column.</summary>
    public const int InvoiceSupplierInvoiceNumber = 128;

    /// <summary>Matches InvoiceConfiguration's Currency column - ISO 4217 codes are always 3 characters.</summary>
    public const int InvoiceCurrency = 3;

    /// <summary>Matches InvoiceNoteConfiguration's Content column.</summary>
    public const int InvoiceNoteContent = 4000;
}
