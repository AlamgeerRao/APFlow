using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using Microsoft.Extensions.Logging;

namespace APFlow.Integrations.DocumentIntelligence;

/// <summary>
/// Document Intelligence-backed implementation of <see cref="IDocumentAnalysisService"/>.
/// Depends on <see cref="IDocumentIntelligenceOperations"/> rather than the SDK
/// directly - see that interface's doc comment for why. Currency reconciliation and
/// confidence-score logging - the logic worth testing - live here.
/// </summary>
public sealed class DocumentAnalysisService : IDocumentAnalysisService
{
    private readonly IDocumentIntelligenceOperations _operations;
    private readonly ILogger<DocumentAnalysisService> _logger;

    /// <summary>Creates a new <see cref="DocumentAnalysisService"/>.</summary>
    internal DocumentAnalysisService(IDocumentIntelligenceOperations operations, ILogger<DocumentAnalysisService> logger)
    {
        _operations = operations;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<InvoiceExtractionResult>> AnalyzeInvoiceAsync(byte[] pdfContent, CancellationToken cancellationToken = default)
    {
        if (pdfContent is null || pdfContent.Length == 0)
        {
            return Result.Failure<InvoiceExtractionResult>(
                new Error("DocumentAnalysis.EmptyDocument", "PDF content must not be empty."));
        }

        try
        {
            var raw = await _operations.AnalyzeInvoiceAsync(pdfContent, cancellationToken);

            LogConfidence("SupplierName", raw.SupplierName.Confidence);
            LogConfidence("SupplierInvoiceNumber", raw.SupplierInvoiceNumber.Confidence);
            LogConfidence("InvoiceDate", raw.InvoiceDate.Confidence);
            LogConfidence("DueDate", raw.DueDate.Confidence);
            LogConfidence("NetAmount", raw.NetAmount.Confidence);
            LogConfidence("Vat", raw.Vat.Confidence);
            LogConfidence("GrossTotal", raw.GrossTotal.Confidence);

            // Document Intelligence attaches a currency code per monetary field, not
            // as one document-level field - reconcile to a single best-effort value,
            // preferring the total (most likely to be present/correct), then the
            // subtotal, then the tax amount.
            var currency = raw.GrossTotal.CurrencyCode ?? raw.NetAmount.CurrencyCode ?? raw.Vat.CurrencyCode;

            var result = new InvoiceExtractionResult(
                SupplierName: new ExtractedField<string?>(raw.SupplierName.Value, raw.SupplierName.Confidence),
                SupplierInvoiceNumber: new ExtractedField<string?>(raw.SupplierInvoiceNumber.Value, raw.SupplierInvoiceNumber.Confidence),
                InvoiceDate: new ExtractedField<DateOnly?>(raw.InvoiceDate.Value, raw.InvoiceDate.Confidence),
                DueDate: new ExtractedField<DateOnly?>(raw.DueDate.Value, raw.DueDate.Confidence),
                Currency: currency,
                NetAmount: new ExtractedField<decimal?>(raw.NetAmount.Amount, raw.NetAmount.Confidence),
                Vat: new ExtractedField<decimal?>(raw.Vat.Amount, raw.Vat.Confidence),
                GrossTotal: new ExtractedField<decimal?>(raw.GrossTotal.Amount, raw.GrossTotal.Confidence));

            _logger.LogInformation(
                "Invoice document analysis completed. SupplierNameFound={SupplierNameFound}, " +
                "InvoiceNumberFound={InvoiceNumberFound}, CurrencyFound={CurrencyFound}, " +
                "GrossTotalFound={GrossTotalFound}.",
                raw.SupplierName.Value is not null,
                raw.SupplierInvoiceNumber.Value is not null,
                currency is not null,
                raw.GrossTotal.Amount is not null);
            // Deliberately NOT logging the actual extracted values (supplier name,
            // invoice number) here - see LogConfidence above for per-field
            // presence/confidence, which already covers diagnostics without
            // including business data. This was raised in WP-008 review as worth
            // confirming against log data-classification/retention standards,
            // especially for enterprise customers; reduced to presence-only pending
            // that confirmation rather than assuming it's fine.

            return Result.Success(result);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Handled gracefully per WP-008 task 7: logged, not rethrown - a Document
            // Intelligence outage, quota limit, or unreadable/corrupt PDF should
            // surface as a failed Result the caller can act on, not crash whatever
            // called this.
            _logger.LogError(ex, "Invoice document analysis failed.");
            return Result.Failure<InvoiceExtractionResult>(
                new Error("DocumentAnalysis.AnalysisFailed", "Failed to analyze the submitted document."));
        }
    }

    private void LogConfidence(string fieldName, double? confidence)
    {
        if (confidence.HasValue)
        {
            _logger.LogInformation("Extracted field {FieldName} with confidence {Confidence:P1}.", fieldName, confidence.Value);
        }
        else
        {
            _logger.LogInformation("Field {FieldName} was not extracted from the document.", fieldName);
        }
    }
}
