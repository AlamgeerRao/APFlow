namespace APFlow.Application.DTOs;

/// <summary>
/// A plain outbound email to send via <see cref="APFlow.Application.Interfaces.IEmailSendService"/>.
/// Deliberately generic (WP-031) - a subject/body/recipient triple, not shaped
/// around the supplier-query use case that first needed it, so WP-040 (remittance
/// email) can construct one of these directly for its own content without any
/// change to this type or to <c>IEmailSendService</c> itself.
/// </summary>
/// <param name="ToAddress">The recipient's email address.</param>
/// <param name="Subject">The email subject line.</param>
/// <param name="Body">
/// The email body, plain text (see <see cref="APFlow.Application.Interfaces.IEmailSendService"/>'s
/// doc comment for why this is plain text only, not HTML, for WP-031's scope).
/// </param>
/// <param name="ToDisplayName">
/// Optional display name for the recipient (e.g. the supplier's name). Purely
/// cosmetic - Graph resolves delivery from <see cref="ToAddress"/> alone.
/// </param>
public sealed record SendEmailRequest(string ToAddress, string Subject, string Body, string? ToDisplayName = null);
