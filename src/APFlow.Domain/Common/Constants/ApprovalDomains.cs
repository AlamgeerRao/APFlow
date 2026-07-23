namespace APFlow.Domain.Common.Constants;

/// <summary>
/// Named constants for <see cref="Entities.ApprovalPolicy.Domain"/> values (WP-051).
/// </summary>
public static class ApprovalDomains
{
    /// <summary>Governs who may execute the CHECKED_READY_TO_APPROVE -&gt; APPROVED invoice transition (WP-051 task 4). Seeded for GB Skips.</summary>
    public const string InvoiceApproval = "InvoiceApproval";

    /// <summary>
    /// Governs who may create a remittance/payment batch (WP-038, not yet built).
    /// Defined now so the domain concept exists per task 1's "extend... alongside
    /// its existing Payment Batch Approval scope" framing - no policy is seeded
    /// against this domain, and no code calls
    /// <c>IApprovalAuthorizationService.AuthorizeApprovalAsync</c> with it yet. See
    /// docs/WP-051-Approval-Policy-Decisions.md task 5 for the intended hook.
    /// </summary>
    public const string PaymentBatchApproval = "PaymentBatchApproval";
}
