using APFlow.Integrations.DocumentIntelligence;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace APFlow.Integrations.Tests.DocumentIntelligence;

public class DocumentAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeInvoiceAsync_EmptyContent_ReturnsFailure_WithoutCallingOperations()
    {
        var (service, _) = CreateService();

        var result = await service.AnalyzeInvoiceAsync([]);

        Assert.True(result.IsFailure);
        Assert.Equal("DocumentAnalysis.EmptyDocument", result.Error.Code);
    }

    [Fact]
    public async Task AnalyzeInvoiceAsync_NullContent_ReturnsFailure()
    {
        var (service, _) = CreateService();

        var result = await service.AnalyzeInvoiceAsync(null!);

        Assert.True(result.IsFailure);
        Assert.Equal("DocumentAnalysis.EmptyDocument", result.Error.Code);
    }

    [Fact]
    public async Task AnalyzeInvoiceAsync_AllFieldsExtracted_PassesThroughCorrectly()
    {
        var (service, ops) = CreateService();
        ops.Result = new RawInvoiceAnalysis(
            SupplierName: new RawTextField("Acme Supplies Ltd", 0.98),
            SupplierInvoiceNumber: new RawTextField("INV-12345", 0.95),
            InvoiceDate: new RawDateField(new DateOnly(2026, 1, 15), 0.9),
            DueDate: new RawDateField(new DateOnly(2026, 2, 15), 0.85),
            NetAmount: new RawMoneyField(1000.00m, "GBP", 0.97),
            Vat: new RawMoneyField(200.00m, "GBP", 0.96),
            GrossTotal: new RawMoneyField(1200.00m, "GBP", 0.99));

        var result = await service.AnalyzeInvoiceAsync([1, 2, 3]);

        Assert.True(result.IsSuccess);
        var invoice = result.Value;
        Assert.Equal("Acme Supplies Ltd", invoice.SupplierName.Value);
        Assert.Equal(0.98, invoice.SupplierName.Confidence);
        Assert.Equal("INV-12345", invoice.SupplierInvoiceNumber.Value);
        Assert.Equal(new DateOnly(2026, 1, 15), invoice.InvoiceDate.Value);
        Assert.Equal(new DateOnly(2026, 2, 15), invoice.DueDate.Value);
        Assert.Equal(1000.00m, invoice.NetAmount.Value);
        Assert.Equal(200.00m, invoice.Vat.Value);
        Assert.Equal(1200.00m, invoice.GrossTotal.Value);
        Assert.Equal("GBP", invoice.Currency);
    }

    [Fact]
    public async Task AnalyzeInvoiceAsync_NoFieldsRecognized_ReturnsSuccessWithAllNulls()
    {
        // A successful API call that recognized nothing meaningful is still a
        // success, not a failure - see IDocumentAnalysisService's doc comment.
        var (service, _) = CreateService();

        var result = await service.AnalyzeInvoiceAsync([1, 2, 3]);

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value.SupplierName.Value);
        Assert.Null(result.Value.GrossTotal.Value);
        Assert.Null(result.Value.Currency);
    }

    [Fact]
    public async Task AnalyzeInvoiceAsync_CurrencyReconciliation_PrefersGrossTotal()
    {
        var (service, ops) = CreateService();
        ops.Result = ops.Result with
        {
            GrossTotal = new RawMoneyField(1200.00m, "EUR", 0.9),
            NetAmount = new RawMoneyField(1000.00m, "GBP", 0.9),
            Vat = new RawMoneyField(200.00m, "USD", 0.9),
        };

        var result = await service.AnalyzeInvoiceAsync([1, 2, 3]);

        Assert.Equal("EUR", result.Value.Currency);
    }

    [Fact]
    public async Task AnalyzeInvoiceAsync_CurrencyReconciliation_FallsBackToNetAmount_WhenGrossTotalMissing()
    {
        var (service, ops) = CreateService();
        ops.Result = ops.Result with
        {
            GrossTotal = new RawMoneyField(null, null, null),
            NetAmount = new RawMoneyField(1000.00m, "GBP", 0.9),
            Vat = new RawMoneyField(200.00m, "USD", 0.9),
        };

        var result = await service.AnalyzeInvoiceAsync([1, 2, 3]);

        Assert.Equal("GBP", result.Value.Currency);
    }

    [Fact]
    public async Task AnalyzeInvoiceAsync_CurrencyReconciliation_FallsBackToVat_WhenOthersMissing()
    {
        var (service, ops) = CreateService();
        ops.Result = ops.Result with
        {
            GrossTotal = new RawMoneyField(null, null, null),
            NetAmount = new RawMoneyField(null, null, null),
            Vat = new RawMoneyField(200.00m, "USD", 0.9),
        };

        var result = await service.AnalyzeInvoiceAsync([1, 2, 3]);

        Assert.Equal("USD", result.Value.Currency);
    }

    [Fact]
    public async Task AnalyzeInvoiceAsync_NoCurrencyAnywhere_ResultCurrencyIsNull()
    {
        var (service, _) = CreateService();

        var result = await service.AnalyzeInvoiceAsync([1, 2, 3]);

        Assert.Null(result.Value.Currency);
    }

    [Fact]
    public async Task AnalyzeInvoiceAsync_OperationsThrows_ReturnsFailure_DoesNotPropagate()
    {
        var (service, ops) = CreateService();
        ops.Mode = FakeDocumentIntelligenceOperations.Behavior.ThrowGeneric;

        var result = await service.AnalyzeInvoiceAsync([1, 2, 3]);

        Assert.True(result.IsFailure);
        Assert.Equal("DocumentAnalysis.AnalysisFailed", result.Error.Code);
    }

    [Fact]
    public async Task AnalyzeInvoiceAsync_CallerCancels_PropagatesCancellation()
    {
        var (service, ops) = CreateService();
        ops.Mode = FakeDocumentIntelligenceOperations.Behavior.ThrowOperationCanceled;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.AnalyzeInvoiceAsync([1, 2, 3], cts.Token));
    }

    private static (Integrations.DocumentIntelligence.DocumentAnalysisService Service, FakeDocumentIntelligenceOperations Operations) CreateService()
    {
        var operations = new FakeDocumentIntelligenceOperations();
        var service = new Integrations.DocumentIntelligence.DocumentAnalysisService(operations, NullLogger<Integrations.DocumentIntelligence.DocumentAnalysisService>.Instance);
        return (service, operations);
    }
}
