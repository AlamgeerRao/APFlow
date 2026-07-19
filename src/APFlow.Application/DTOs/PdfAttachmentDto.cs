namespace APFlow.Application.DTOs;

/// <summary>
/// A single extracted PDF attachment. Deliberately content-only, no interpretation -
/// OCR, invoice validation, and duplicate detection are all explicitly out of scope
/// for WP-007 and must not be added to this shape without a real requirement driving it.
/// </summary>
/// <param name="FileName">The attachment's file name as reported by Graph, e.g. "invoice.pdf".</param>
/// <param name="SizeInBytes">The attachment's size in bytes, as reported by Graph.</param>
/// <param name="ContentType">The attachment's MIME content type, e.g. "application/pdf".</param>
/// <param name="Content">
/// The raw PDF bytes. Represented as <c>byte[]</c> rather than <see cref="System.IO.Stream"/> -
/// Graph's file attachment API already returns fully-materialized bytes (not a
/// stream) for this size of content, and byte[] avoids disposal semantics leaking
/// into every caller/test. If a future work package needs true streaming (very large
/// attachments), that's a deliberate interface change, not something to guess at now.
/// </param>
public sealed record PdfAttachmentDto(
    string FileName,
    long SizeInBytes,
    string ContentType,
    byte[] Content);
