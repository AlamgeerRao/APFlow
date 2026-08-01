namespace APFlow.Application.DTOs;

/// <summary>
/// Lightweight metadata (no content bytes) for a single attachment Graph reported on
/// an email, regardless of whether it was extractable as a PDF - added by WP-076 so
/// a caller can record what WAS on an email that had no processable PDF (e.g. "a
/// .png was the only attachment"), which <see cref="PdfAttachmentDto"/> alone cannot
/// answer, since it only ever describes PDF survivors.
/// </summary>
/// <param name="FileName">The attachment's file name as reported by Graph.</param>
/// <param name="ContentType">The attachment's MIME content type as reported by Graph.</param>
public sealed record AttachmentInfoDto(string FileName, string ContentType);

/// <summary>
/// Result of considering every attachment on an email (WP-076): the PDF survivors
/// (unchanged from WP-007's original single-list shape) plus lightweight metadata
/// for every attachment Graph reported, PDF or not. <see cref="AllAttachments"/>
/// exists purely so a caller can describe what was found when
/// <see cref="PdfAttachments"/> is empty - it is not a second extraction pass, just
/// the same Graph response's metadata surfaced alongside the filtered result.
/// </summary>
/// <param name="PdfAttachments">The extracted PDF attachments - same content and meaning as WP-007's original return shape.</param>
/// <param name="AllAttachments">Filename/content-type for every attachment Graph reported on the email, including inline and non-PDF ones.</param>
public sealed record AttachmentExtractionResult(
    IReadOnlyList<PdfAttachmentDto> PdfAttachments,
    IReadOnlyList<AttachmentInfoDto> AllAttachments);
