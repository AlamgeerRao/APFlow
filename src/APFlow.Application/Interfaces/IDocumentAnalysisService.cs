using APFlow.Application.DTOs;
using APFlow.Domain.Common;

namespace APFlow.Application.Interfaces;

/// <summary>
/// Analyzes PDF invoice documents via Azure AI Document Intelligence (see
/// APFlow.Integrations.DocumentIntelligence.DocumentAnalysisService). WP-008 scope:
/// submits a PDF for analysis using the prebuilt-invoice model and returns a
/// strongly typed <see cref="InvoiceExtractionResult"/>. Does not persist anything to
/// a database, does not touch any UI, and does not implement or feed an approval
/// workflow - all explicitly out of scope. This service hands back raw extracted
/// data; what a caller does with it (validation, persistence, review) is a future
/// work package's concern.
/// </summary>
public interface IDocumentAnalysisService
{
    /// <summary>
    /// Submits the given PDF content for invoice analysis. A successful
    /// <see cref="Result"/> can still contain a mostly-null
    /// <see cref="InvoiceExtractionResult"/> if Document Intelligence didn't
    /// recognize the document as an invoice or couldn't extract particular fields -
    /// that is not treated as a failure. Only a genuine failure to submit/complete
    /// the analysis (service unreachable, corrupt/unreadable PDF, auth failure)
    /// returns a failed <see cref="Result"/>.
    /// </summary>
    Task<Result<InvoiceExtractionResult>> AnalyzeInvoiceAsync(byte[] pdfContent, CancellationToken cancellationToken = default);
}
