namespace APFlow.Domain.Common.Constants;

/// <summary>
/// Cross-cutting HTTP header names used across the solution. This is a foundation-level
/// constants class for platform concerns only (tenancy, correlation) — feature-specific
/// constants belong in the owning feature's folder, not here.
/// </summary>
public static class HeaderNames
{
    /// <summary>Header used to propagate the tenant identifier, per the multi-tenant isolation principle.</summary>
    public const string TenantId = "X-Tenant-Id";

    /// <summary>Header used to correlate a request across logs and services.</summary>
    public const string CorrelationId = "X-Correlation-Id";
}
