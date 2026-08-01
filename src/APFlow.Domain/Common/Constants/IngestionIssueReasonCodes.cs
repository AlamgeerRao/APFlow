namespace APFlow.Domain.Common.Constants;

/// <summary>
/// Named constants for <see cref="APFlow.Domain.Entities.IngestionIssue.ReasonCode"/>
/// (WP-076). A plain string on the entity, not an enum - same reasoning as
/// <see cref="InvoiceStatusCodes"/>: this class is a convenience for known values,
/// not the source of truth. Only one reason exists today; more may be added as new
/// ingestion failure modes are identified.
/// </summary>
public static class IngestionIssueReasonCodes
{
    /// <summary>An email arrived with no attachment that could be extracted as a processable PDF invoice.</summary>
    public const string NoProcessableAttachments = "NO_PROCESSABLE_ATTACHMENTS";
}
