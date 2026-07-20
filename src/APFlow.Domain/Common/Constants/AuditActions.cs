using APFlow.Domain.Entities;

namespace APFlow.Domain.Common.Constants;

/// <summary>
/// Named constants for well-known <see cref="AuditLog.Action"/> values.
/// <see cref="AuditLog.Action"/> itself stays a plain string (see that
/// entity's doc comment for why), so this class exists only so real callers write a
/// named constant instead of a raw string literal - it is not an exhaustive or
/// closed set, and new values do not need to be added here before use.
/// </summary>
public static class AuditActions
{
    /// <summary>An invoice's <c>Status</c> field changed (see <c>InvoiceService.UpdateAsync</c>, WP-013 task 4).</summary>
    public const string InvoiceStatusChanged = "InvoiceStatusChanged";
}
