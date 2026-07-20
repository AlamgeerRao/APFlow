using APFlow.Application.DTOs;
using APFlow.Application.Features.Invoices;
using APFlow.Application.Features.Suppliers;
using APFlow.Application.Interfaces;
using APFlow.Application.Tests.Features;
using APFlow.Domain.Common;
using APFlow.Domain.Entities;
using APFlow.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace APFlow.Application.Tests.Features.Invoices;

public class InvoiceProcessingServiceTests
{
    private const string MessageId = "graph-message-1";
    private const string FileName = "invoice.pdf";

    [Fact]
    public async Task ProcessUnreadEmailsAsync_HappyPath_CreatesSupplierAndInvoice_MarksEmailProcessed()
    {
        var (service, deps) = CreateService();
        deps.EmailSync.UnreadEmails.Add(NewEmail());
        deps.PdfExtraction.AttachmentsByMessageId[MessageId] = [NewAttachment()];
        deps.DocumentAnalysis.NextResult = NewExtraction(supplierName: "Acme Ltd");

        var result = await service.ProcessUnreadEmailsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.EmailsSynced);
        Assert.Equal(1, result.Value.EmailsMarkedProcessed);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(InvoiceProcessingOutcome.Processed, item.Outcome);
        Assert.NotNull(item.InvoiceId);
        Assert.False(item.IsPotentialDuplicate);

        var supplier = Assert.Single(deps.SupplierRepository.Suppliers);
        Assert.Equal("Acme Ltd", supplier.Name);

        var invoice = Assert.Single(deps.InvoiceRepository.Invoices);
        Assert.Equal(supplier.Id, invoice.SupplierId);
        Assert.Equal(InvoiceStatus.Extracted, invoice.Status);
        Assert.Equal("invoices/graph-message-1/invoice.pdf", invoice.SourceDocumentBlobName);
        Assert.Equal(MessageId, invoice.SourceEmailMessageId);

        Assert.Single(deps.BlobStorage.UploadedBlobNames);
        Assert.Contains(MessageId, deps.EmailSync.MarkedAsProcessedMessageIds);
    }

    [Fact]
    public async Task ProcessUnreadEmailsAsync_RunTwiceOverSameInput_SecondRunSkipsAlreadyProcessedAttachment()
    {
        var (service, deps) = CreateService();
        deps.EmailSync.UnreadEmails.Add(NewEmail());
        deps.PdfExtraction.AttachmentsByMessageId[MessageId] = [NewAttachment()];
        deps.DocumentAnalysis.NextResult = NewExtraction(supplierName: "Acme Ltd");

        var firstRun = await service.ProcessUnreadEmailsAsync();
        var secondRun = await service.ProcessUnreadEmailsAsync();

        Assert.True(firstRun.IsSuccess);
        Assert.True(secondRun.IsSuccess);

        var secondItem = Assert.Single(secondRun.Value.Items);
        Assert.Equal(InvoiceProcessingOutcome.AlreadyProcessed, secondItem.Outcome);
        Assert.Equal(Assert.Single(firstRun.Value.Items).InvoiceId, secondItem.InvoiceId);

        // Idempotency proof: no second blob upload, no second invoice row, no second
        // supplier row - despite the exact same email/attachment being synced twice.
        Assert.Single(deps.BlobStorage.UploadedBlobNames);
        Assert.Single(deps.InvoiceRepository.Invoices);
        Assert.Single(deps.SupplierRepository.Suppliers);
    }

    [Fact]
    public async Task ProcessUnreadEmailsAsync_SupplierAlreadyExists_ReusesSupplier_CaseInsensitiveTrimmedMatch()
    {
        var (service, deps) = CreateService();
        var existingSupplier = new Supplier { Name = "Acme Ltd" };
        deps.SupplierRepository.Suppliers.Add(existingSupplier);

        deps.EmailSync.UnreadEmails.Add(NewEmail());
        deps.PdfExtraction.AttachmentsByMessageId[MessageId] = [NewAttachment()];
        deps.DocumentAnalysis.NextResult = NewExtraction(supplierName: "  acme ltd  "); // different case/whitespace

        var result = await service.ProcessUnreadEmailsAsync();

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(InvoiceProcessingOutcome.Processed, item.Outcome);

        Assert.Single(deps.SupplierRepository.Suppliers); // no new supplier created
        var invoice = Assert.Single(deps.InvoiceRepository.Invoices);
        Assert.Equal(existingSupplier.Id, invoice.SupplierId);
    }

    [Fact]
    public async Task ProcessUnreadEmailsAsync_NoSupplierNameExtracted_ItemFails_NoSupplierOrInvoiceCreated()
    {
        var (service, deps) = CreateService();
        deps.EmailSync.UnreadEmails.Add(NewEmail());
        deps.PdfExtraction.AttachmentsByMessageId[MessageId] = [NewAttachment()];
        deps.DocumentAnalysis.NextResult = NewExtraction(supplierName: null);

        var result = await service.ProcessUnreadEmailsAsync();

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(InvoiceProcessingOutcome.Failed, item.Outcome);
        Assert.Equal("InvoiceProcessing.SupplierNameNotExtracted", item.ErrorCode);
        Assert.Null(item.InvoiceId);

        Assert.Empty(deps.SupplierRepository.Suppliers);
        Assert.Empty(deps.InvoiceRepository.Invoices);
        Assert.DoesNotContain(MessageId, deps.EmailSync.MarkedAsProcessedMessageIds); // email left unread for retry
    }

    [Fact]
    public async Task ProcessUnreadEmailsAsync_DuplicateFlagged_InvoiceStillSaved_FlagSurfacedInResult()
    {
        var (service, deps) = CreateService();
        var supplier = new Supplier { Name = "Acme Ltd" };
        deps.SupplierRepository.Suppliers.Add(supplier);
        var existingInvoiceId = Guid.NewGuid();

        deps.DuplicateDetection.ResultFactory = id => Result.Success(new DuplicateCheckResult(
            id, true, [new DuplicateMatch(existingInvoiceId, "INV-1", ["SupplierId", "SupplierInvoiceNumber", "InvoiceDate", "GrossTotal"], "All fields matched.")]));

        deps.EmailSync.UnreadEmails.Add(NewEmail());
        deps.PdfExtraction.AttachmentsByMessageId[MessageId] = [NewAttachment()];
        deps.DocumentAnalysis.NextResult = NewExtraction(supplierName: "Acme Ltd");

        var result = await service.ProcessUnreadEmailsAsync();

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(InvoiceProcessingOutcome.Processed, item.Outcome); // still saved, not blocked
        Assert.True(item.IsPotentialDuplicate);
        Assert.NotNull(item.InvoiceId);
    }

    [Fact]
    public async Task ProcessUnreadEmailsAsync_DuplicateCheckFails_ItemStillProcessed_FlagIsNull()
    {
        var (service, deps) = CreateService();
        deps.DuplicateDetection.ResultFactory = _ => Result.Failure<DuplicateCheckResult>(new Error("Invoice.NotFound", "not found"));

        deps.EmailSync.UnreadEmails.Add(NewEmail());
        deps.PdfExtraction.AttachmentsByMessageId[MessageId] = [NewAttachment()];
        deps.DocumentAnalysis.NextResult = NewExtraction(supplierName: "Acme Ltd");

        var result = await service.ProcessUnreadEmailsAsync();

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(InvoiceProcessingOutcome.Processed, item.Outcome);
        Assert.Null(item.IsPotentialDuplicate);
        Assert.NotNull(item.InvoiceId);
    }

    [Fact]
    public async Task ProcessUnreadEmailsAsync_EmailSyncFails_ReturnsFailure()
    {
        var (service, deps) = CreateService();
        deps.EmailSync.SyncResultOverride = Result.Failure<IReadOnlyList<EmailSummaryDto>>(new Error("Graph.Unreachable", "boom"));

        var result = await service.ProcessUnreadEmailsAsync();

        Assert.True(result.IsFailure);
        Assert.Equal("Graph.Unreachable", result.Error.Code);
    }

    [Fact]
    public async Task ProcessUnreadEmailsAsync_PdfExtractionFails_ItemFailed_EmailNotMarkedProcessed()
    {
        var (service, deps) = CreateService();
        deps.EmailSync.UnreadEmails.Add(NewEmail());
        deps.PdfExtraction.FailuresByMessageId[MessageId] = new Error("Graph.AttachmentReadFailed", "boom");

        var result = await service.ProcessUnreadEmailsAsync();

        Assert.True(result.IsSuccess); // batch-level call still succeeds; failure is per-item
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(InvoiceProcessingOutcome.Failed, item.Outcome);
        Assert.Null(item.FileName);
        Assert.Equal(0, result.Value.EmailsMarkedProcessed);
        Assert.DoesNotContain(MessageId, deps.EmailSync.MarkedAsProcessedMessageIds);
    }

    [Fact]
    public async Task ProcessUnreadEmailsAsync_NoAttachments_EmailMarkedProcessed_NoItems()
    {
        var (service, deps) = CreateService();
        deps.EmailSync.UnreadEmails.Add(NewEmail());
        // No entry in AttachmentsByMessageId -> fake returns an empty list, matching
        // IPdfExtractionService's documented "zero attachments is success" behavior.

        var result = await service.ProcessUnreadEmailsAsync();

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(1, result.Value.EmailsMarkedProcessed);
    }

    [Fact]
    public async Task ProcessUnreadEmailsAsync_OneOfTwoAttachmentsFails_PartialSuccess_EmailNotMarkedProcessed()
    {
        var (service, deps) = CreateService();
        deps.EmailSync.UnreadEmails.Add(NewEmail());
        deps.PdfExtraction.AttachmentsByMessageId[MessageId] =
        [
            NewAttachment("good.pdf"),
            NewAttachment("bad.pdf"),
        ];
        deps.DocumentAnalysis.NextResult = NewExtraction(supplierName: "Acme Ltd");
        deps.BlobStorage.UploadFailuresByBlobNameContains["bad.pdf"] = new Error("BlobStorage.UploadFailed", "boom");

        var result = await service.ProcessUnreadEmailsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Contains(result.Value.Items, i => i.FileName == "good.pdf" && i.Outcome == InvoiceProcessingOutcome.Processed);
        Assert.Contains(result.Value.Items, i => i.FileName == "bad.pdf" && i.Outcome == InvoiceProcessingOutcome.Failed);
        Assert.Equal(0, result.Value.EmailsMarkedProcessed);
        Assert.DoesNotContain(MessageId, deps.EmailSync.MarkedAsProcessedMessageIds);
    }

    private static (InvoiceProcessingService Service, TestDependencies Dependencies) CreateService()
    {
        var invoiceRepository = new FakeInvoiceRepository();
        var supplierRepository = new FakeSupplierRepository();
        var invoiceService = new InvoiceService(invoiceRepository, supplierRepository, NullLogger<InvoiceService>.Instance);
        var supplierService = new SupplierService(supplierRepository, NullLogger<SupplierService>.Instance);

        var emailSync = new FakeEmailSyncService();
        var pdfExtraction = new FakePdfExtractionService();
        var blobStorage = new FakeBlobStorageService();
        var documentAnalysis = new FakeDocumentAnalysisService();
        var duplicateDetection = new FakeDuplicateDetectionService();

        var service = new InvoiceProcessingService(
            emailSync, pdfExtraction, blobStorage, documentAnalysis, duplicateDetection,
            invoiceService, supplierService, NullLogger<InvoiceProcessingService>.Instance);

        return (service, new TestDependencies(
            invoiceRepository, supplierRepository, emailSync, pdfExtraction, blobStorage, documentAnalysis, duplicateDetection));
    }

    private static EmailSummaryDto NewEmail() => new(MessageId, "Invoice attached", "supplier@example.com", "Acme Ltd", DateTimeOffset.UtcNow);

    private static PdfAttachmentDto NewAttachment(string fileName = FileName) => new(fileName, 1024, "application/pdf", [1, 2, 3]);

    private static InvoiceExtractionResult NewExtraction(string? supplierName) => new(
        SupplierName: new ExtractedField<string?>(supplierName, 0.95),
        SupplierInvoiceNumber: new ExtractedField<string?>("INV-1", 0.95),
        InvoiceDate: new ExtractedField<DateOnly?>(new DateOnly(2026, 1, 1), 0.95),
        DueDate: new ExtractedField<DateOnly?>(new DateOnly(2026, 2, 1), 0.95),
        Currency: "GBP",
        NetAmount: new ExtractedField<decimal?>(100m, 0.95),
        Vat: new ExtractedField<decimal?>(20m, 0.95),
        GrossTotal: new ExtractedField<decimal?>(120m, 0.95));

    private sealed record TestDependencies(
        FakeInvoiceRepository InvoiceRepository,
        FakeSupplierRepository SupplierRepository,
        FakeEmailSyncService EmailSync,
        FakePdfExtractionService PdfExtraction,
        FakeBlobStorageService BlobStorage,
        FakeDocumentAnalysisService DocumentAnalysis,
        FakeDuplicateDetectionService DuplicateDetection);
}
