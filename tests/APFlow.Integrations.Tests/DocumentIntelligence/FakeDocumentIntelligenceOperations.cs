using APFlow.Integrations.DocumentIntelligence;

namespace APFlow.Integrations.Tests.DocumentIntelligence;

/// <summary>
/// Hand-written fake for <see cref="IDocumentIntelligenceOperations"/>, fully
/// controlled by this test project - same reasoning as every prior WP's Graph fakes.
/// </summary>
internal sealed class FakeDocumentIntelligenceOperations : IDocumentIntelligenceOperations
{
    public enum Behavior
    {
        Succeed,
        ThrowGeneric,
        ThrowOperationCanceled,
    }

    public Behavior Mode { get; set; } = Behavior.Succeed;

    public RawInvoiceAnalysis Result { get; set; } = new(
        SupplierName: new RawTextField(null, null),
        SupplierInvoiceNumber: new RawTextField(null, null),
        InvoiceDate: new RawDateField(null, null),
        DueDate: new RawDateField(null, null),
        NetAmount: new RawMoneyField(null, null, null),
        Vat: new RawMoneyField(null, null, null),
        GrossTotal: new RawMoneyField(null, null, null));

    public Task<RawInvoiceAnalysis> AnalyzeInvoiceAsync(byte[] pdfContent, CancellationToken cancellationToken) => Mode switch
    {
        Behavior.Succeed => Task.FromResult(Result),
        Behavior.ThrowOperationCanceled => throw new OperationCanceledException(cancellationToken),
        _ => throw new InvalidOperationException("Simulated Document Intelligence failure."),
    };
}
