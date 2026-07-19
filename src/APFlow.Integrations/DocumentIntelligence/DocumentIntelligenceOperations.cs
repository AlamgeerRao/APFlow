using Azure;
using Azure.AI.DocumentIntelligence;
using Microsoft.Extensions.Logging;

namespace APFlow.Integrations.DocumentIntelligence;

/// <summary>
/// Thin seam between <see cref="DocumentAnalysisService"/> and the Document
/// Intelligence SDK. Same testability reasoning as every prior WP's Graph seams: this
/// project cannot reliably fake Document Intelligence's client types without a real
/// package to verify against, so this interface is fully hand-written and owned here.
/// All currency reconciliation, confidence logging, and Result wrapping lives in
/// DocumentAnalysisService instead, specifically so it's covered by real, fake-based
/// unit tests.
/// </summary>
internal interface IDocumentIntelligenceOperations
{
    /// <summary>Submits the given PDF for prebuilt-invoice analysis and returns the raw extracted fields.</summary>
    Task<RawInvoiceAnalysis> AnalyzeInvoiceAsync(byte[] pdfContent, CancellationToken cancellationToken);
}

/// <summary>
/// Real implementation of <see cref="IDocumentIntelligenceOperations"/>, wrapping
/// Azure.AI.DocumentIntelligence's prebuilt-invoice model.
/// VERIFICATION STATUS (updated after WP-008 review): the API shape used below was
/// initially written from training knowledge only and flagged as the
/// least-verified code in this codebase. It has since been checked against real
/// Microsoft documentation and SDK samples (web search, not a local package
/// restore - dotnet build in this sandbox still cannot run against the real
/// NuGet-hosted package):
/// - AnalyzeDocumentAsync(WaitUntil, string modelId, BinaryData, CancellationToken)
///   is a real, documented overload - confirmed via
///   learn.microsoft.com/en-us/dotnet/api/azure.ai.documentintelligence.documentintelligenceclient.analyzedocumentasync
/// - DocumentField.ValueString, .ValueDate, .ValueCurrency, and .Confidence are all
///   real, documented properties - confirmed via the same page and multiple
///   Azure-published samples (github.com/Azure/azure-sdk-for-net prebuilt-invoice
///   samples; a Microsoft Learn Succinctly e-book explicitly listing ValueType,
///   ValueString, ValueDate, and ValueCurrency as DocumentField's typed accessors).
/// - CurrencyValue.Amount and .CurrencyCode are real, documented properties -
///   confirmed via the same samples and the Python SDK's migration guide (documents
///   the identical field shape: amount/currencySymbol/currencyCode).
/// - DocumentIntelligenceClient(Uri, AzureKeyCredential) is a real, documented
///   constructor - confirmed via the same samples. The DefaultAzureCredential
///   overload used in DependencyInjection.cs was not independently found in search
///   results but follows the same (Uri, TokenCredential) pattern every other Azure
///   SDK client in this codebase (GraphServiceClient, BlobServiceClient) uses.
/// One genuine finding from this research: the SDK renamed DocumentField's type
/// discriminator from ".Type" (beta) to ".FieldType" (GA v1.0.0) between versions -
/// this code is unaffected since it never reads that discriminator (each field read
/// is wrapped in try/catch instead - see below), but it's a concrete example of why
/// this class's per-field defensiveness is the right posture regardless of how
/// thoroughly the shape is checked in advance.
/// Residual risk: package version drift (exact 1.0.0 vs a patch release) and
/// anything not covered by the samples found. Still worth treating a build error
/// here as more likely than elsewhere in this codebase, just less likely than
/// before this research.
/// NOT independently unit-tested, for the same reason as every prior WP's mechanical
/// SDK wrapper.
/// </summary>
internal sealed class DocumentIntelligenceOperations : IDocumentIntelligenceOperations
{
    private const string InvoiceModelId = "prebuilt-invoice";

    private readonly DocumentIntelligenceClient _client;
    private readonly Microsoft.Extensions.Logging.ILogger<DocumentIntelligenceOperations> _logger;

    public DocumentIntelligenceOperations(DocumentIntelligenceClient client, Microsoft.Extensions.Logging.ILogger<DocumentIntelligenceOperations> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task<RawInvoiceAnalysis> AnalyzeInvoiceAsync(byte[] pdfContent, CancellationToken cancellationToken)
    {
        var operation = await _client.AnalyzeDocumentAsync(
            WaitUntil.Completed,
            InvoiceModelId,
            BinaryData.FromBytes(pdfContent),
            cancellationToken: cancellationToken);

        var document = operation.Value.Documents.Count > 0 ? operation.Value.Documents[0] : null;
        var fields = document?.Fields;

        return new RawInvoiceAnalysis(
            SupplierName: ReadText(fields, "VendorName"),
            SupplierInvoiceNumber: ReadText(fields, "InvoiceId"),
            InvoiceDate: ReadDate(fields, "InvoiceDate"),
            DueDate: ReadDate(fields, "DueDate"),
            NetAmount: ReadMoney(fields, "SubTotal"),
            Vat: ReadMoney(fields, "TotalTax"),
            GrossTotal: ReadMoney(fields, "InvoiceTotal"));
    }

    // Each field read is individually defensive (try/catch, not just null-checked):
    // given the SDK-shape uncertainty documented at the class level, one
    // unexpectedly-typed field should not crash the entire analysis. A field that
    // fails to parse is logged and treated the same as a field that wasn't found -
    // null value, null confidence - not a whole-document failure.

    private RawTextField ReadText(IReadOnlyDictionary<string, DocumentField>? fields, string fieldName)
    {
        if (fields is null || !fields.TryGetValue(fieldName, out var field))
        {
            return new RawTextField(null, null);
        }

        try
        {
            return new RawTextField(field.ValueString, field.Confidence);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read text field {FieldName} from Document Intelligence result.", fieldName);
            return new RawTextField(null, null);
        }
    }

    private RawDateField ReadDate(IReadOnlyDictionary<string, DocumentField>? fields, string fieldName)
    {
        if (fields is null || !fields.TryGetValue(fieldName, out var field))
        {
            return new RawDateField(null, null);
        }

        try
        {
            var date = field.ValueDate.HasValue ? DateOnly.FromDateTime(field.ValueDate.Value.DateTime) : (DateOnly?)null;
            return new RawDateField(date, field.Confidence);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read date field {FieldName} from Document Intelligence result.", fieldName);
            return new RawDateField(null, null);
        }
    }

    private RawMoneyField ReadMoney(IReadOnlyDictionary<string, DocumentField>? fields, string fieldName)
    {
        if (fields is null || !fields.TryGetValue(fieldName, out var field))
        {
            return new RawMoneyField(null, null, null);
        }

        try
        {
            var currencyValue = field.ValueCurrency;
            decimal? amount = currencyValue is not null ? (decimal)currencyValue.Amount : null;
            var currencyCode = currencyValue?.CurrencyCode;

            return new RawMoneyField(amount, currencyCode, field.Confidence);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read monetary field {FieldName} from Document Intelligence result.", fieldName);
            return new RawMoneyField(null, null, null);
        }
    }
}
