namespace APFlow.Domain.Common.Constants;

/// <summary>Named constants for <see cref="Entities.WorkflowTemplate.DomainName"/> values (WP-050).</summary>
public static class WorkflowDomains
{
    /// <summary>The only domain WP-050 seeds a workflow for. Other domains (e.g. a future Supplier workflow) are not precluded by the schema, just not built yet.</summary>
    public const string Invoice = "Invoice";
}
