using APFlow.Application.DTOs;
using APFlow.Application.Features.Audit;
using APFlow.Application.Features.Invoices;
using APFlow.Application.Features.Suppliers;
using APFlow.Application.Interfaces;
using APFlow.Application.Tests.Features;
using APFlow.Domain.Common;
using APFlow.Domain.Entities;
using APFlow.Domain.Common.Constants;
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
        Assert.Equal(InvoiceStatusCodes.Extracted, invoice.Status);
        Assert.Equal("invoices/graph-message-1/invoice.pdf", invoice.SourceDocumentBlobName);
        Assert.Equal(MessageId, invoice.SourceEmailMessageId);
        Assert.False(invoice.IsPotentialDuplicate); // persisted, not just reported - see WP-010's ruling
        Assert.Null(invoice.DuplicateCheckReason);

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

        deps.DuplicateDetection.ResultFactory = (candidate, _) => new DuplicateCheckResult(
            candidate.Id, true, [new DuplicateMatch(existingInvoiceId, "INV-1", ["Supplier", "InvoiceNumber"], "Matches existing invoice on Supplier and Invoice Number.")]);

        deps.EmailSync.UnreadEmails.Add(NewEmail());
        deps.PdfExtraction.AttachmentsByMessageId[MessageId] = [NewAttachment()];
        deps.DocumentAnalysis.NextResult = NewExtraction(supplierName: "Acme Ltd");

        var result = await service.ProcessUnreadEmailsAsync();

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(InvoiceProcessingOutcome.Processed, item.Outcome); // still saved, not blocked
        Assert.True(item.IsPotentialDuplicate);
        Assert.NotNull(item.InvoiceId);

        // Persisted, not just reported in the per-run result - see WP-010's ruling,
        // now via IInvoiceRepository.PersistDuplicateCheckResultAsync (WP-048).
        var invoice = Assert.Single(deps.InvoiceRepository.Invoices);
        Assert.True(invoice.IsPotentialDuplicate);
        Assert.Equal("Matches existing invoice on Supplier and Invoice Number.", invoice.DuplicateCheckReason);
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

    [Fact]
    public async Task ProcessUnreadEmailsAsync_AtomicSaveThrows_ItemFailed_BatchStillSucceeds()
    {
        // WP-049 task 3: a failure in the duplicate-check/save step (e.g. a
        // transient database error) must not fail the whole ingestion batch.
        var (service, deps) = CreateService();
        deps.EmailSync.UnreadEmails.Add(NewEmail());
        deps.PdfExtraction.AttachmentsByMessageId[MessageId] = [NewAttachment()];
        deps.DocumentAnalysis.NextResult = NewExtraction(supplierName: "Acme Ltd");
        deps.InvoiceRepository.SaveChangesExceptionFactory = () => new InvalidOperationException("transient database error");

        var result = await service.ProcessUnreadEmailsAsync();

        Assert.True(result.IsSuccess); // the batch-level call itself did not throw/fail
        var item = Assert.Single(result.Value.Items);
        Assert.Equal(InvoiceProcessingOutcome.Failed, item.Outcome);
        Assert.Equal("InvoiceProcessing.SaveFailed", item.ErrorCode);
        Assert.DoesNotContain(MessageId, deps.EmailSync.MarkedAsProcessedMessageIds); // left unread for retry
    }

    [Fact]
    public async Task ProcessUnreadEmailsAsync_FirstEmailSaveFails_SecondEmailStillProcessedSuccessfully()
    {
        // Stronger proof of "doesn't fail the whole batch": a SEPARATE, unrelated
        // email in the same run succeeds despite an earlier one failing.
        var (service, deps) = CreateService();
        const string firstMessageId = "graph-message-first";
        const string secondMessageId = "graph-message-second";
        deps.EmailSync.UnreadEmails.Add(new EmailSummaryDto(firstMessageId, "Invoice 1", "supplier@example.com", "Acme Ltd", DateTimeOffset.UtcNow));
        deps.EmailSync.UnreadEmails.Add(new EmailSummaryDto(secondMessageId, "Invoice 2", "supplier@example.com", "Acme Ltd", DateTimeOffset.UtcNow));
        deps.PdfExtraction.AttachmentsByMessageId[firstMessageId] = [NewAttachment("first.pdf")];
        deps.PdfExtraction.AttachmentsByMessageId[secondMessageId] = [NewAttachment("second.pdf")];
        deps.DocumentAnalysis.NextResult = NewExtraction(supplierName: "Acme Ltd");

        var callCount = 0;
        deps.InvoiceRepository.SaveChangesExceptionFactory = () =>
        {
            callCount++;
            return callCount == 1 ? new InvalidOperationException("transient database error") : null;
        };

        var result = await service.ProcessUnreadEmailsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Contains(result.Value.Items, i => i.MessageId == firstMessageId && i.Outcome == InvoiceProcessingOutcome.Failed);
        Assert.Contains(result.Value.Items, i => i.MessageId == secondMessageId && i.Outcome == InvoiceProcessingOutcome.Processed);
        Assert.DoesNotContain(firstMessageId, deps.EmailSync.MarkedAsProcessedMessageIds);
        Assert.Contains(secondMessageId, deps.EmailSync.MarkedAsProcessedMessageIds);
    }

    [Fact]
    public async Task ProcessUnreadEmailsAsync_SameFileNameDifferentContent_BothProcessed()
    {
        // WP-052 Part B required scenario: two attachments sharing a file name but
        // with genuinely different content are NOT deduplicated - the old
        // blob-name-based key (messageId + fileName) would have collided here,
        // silently dropping the second one. The content-hash key does not.
        var (service, deps) = CreateService();
        deps.EmailSync.UnreadEmails.Add(NewEmail());
        deps.PdfExtraction.AttachmentsByMessageId[MessageId] =
        [
            new PdfAttachmentDto("invoice.pdf", 1024, "application/pdf", [1, 2, 3]),
            new PdfAttachmentDto("invoice.pdf", 1024, "application/pdf", [4, 5, 6]), // same name, different bytes
        ];
        deps.DocumentAnalysis.NextResult = NewExtraction(supplierName: "Acme Ltd");

        var result = await service.ProcessUnreadEmailsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.All(result.Value.Items, item => Assert.Equal(InvoiceProcessingOutcome.Processed, item.Outcome));
        Assert.Equal(2, deps.InvoiceRepository.Invoices.Count);
        Assert.Equal(2, deps.InvoiceRepository.Invoices.Select(i => i.SourceDocumentContentHash).Distinct().Count());
    }

    [Fact]
    public async Task ProcessUnreadEmailsAsync_DifferentFileNamesSameContent_SecondDeduplicated()
    {
        // WP-052 Part B required scenario: two attachments with different file
        // names but IDENTICAL content are deduplicated - the dedup key follows the
        // document's actual bytes, not what either copy happens to be named.
        var (service, deps) = CreateService();
        var identicalContent = System.Text.Encoding.UTF8.GetBytes("identical-pdf-bytes");
        deps.EmailSync.UnreadEmails.Add(NewEmail());
        deps.PdfExtraction.AttachmentsByMessageId[MessageId] =
        [
            new PdfAttachmentDto("copy-a.pdf", 1024, "application/pdf", identicalContent),
            new PdfAttachmentDto("copy-b.pdf", 1024, "application/pdf", identicalContent),
        ];
        deps.DocumentAnalysis.NextResult = NewExtraction(supplierName: "Acme Ltd");

        var result = await service.ProcessUnreadEmailsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.Contains(result.Value.Items, i => i.FileName == "copy-a.pdf" && i.Outcome == InvoiceProcessingOutcome.Processed);
        Assert.Contains(result.Value.Items, i => i.FileName == "copy-b.pdf" && i.Outcome == InvoiceProcessingOutcome.AlreadyProcessed);
        Assert.Single(deps.InvoiceRepository.Invoices); // only one invoice row created, despite two attachments
    }

    private static (InvoiceProcessingService Service, TestDependencies Dependencies) CreateService()
    {
        var invoiceRepository = new FakeInvoiceRepository();
        var supplierRepository = new FakeSupplierRepository();
        var auditLogRepository = new FakeAuditLogRepository();
        var auditService = new AuditService(auditLogRepository, NullLogger<AuditService>.Instance);
        var currentUserService = new FakeCurrentUserService();
        var approvalAuthorizationService = new FakeApprovalAuthorizationService(); // not exercised - InvoiceProcessingService no longer calls InvoiceService.UpdateAsync (WP-049)
        var invoiceService = new InvoiceService(
            invoiceRepository, supplierRepository, auditService, currentUserService, approvalAuthorizationService,
            new FakeWorkflowValidationService(), NullLogger<InvoiceService>.Instance);
        var supplierService = new SupplierService(supplierRepository, NullLogger<SupplierService>.Instance);

        var emailSync = new FakeEmailSyncService();
        var pdfExtraction = new FakePdfExtractionService();
        var blobStorage = new FakeBlobStorageService();
        var documentAnalysis = new FakeDocumentAnalysisService();
        var duplicateDetection = new FakeDuplicateDetectionService();

        var service = new InvoiceProcessingService(
            emailSync, pdfExtraction, blobStorage, documentAnalysis, duplicateDetection,
            invoiceService, supplierService, invoiceRepository, NullLogger<InvoiceProcessingService>.Instance);

        return (service, new TestDependencies(
            invoiceRepository, supplierRepository, emailSync, pdfExtraction, blobStorage, documentAnalysis, duplicateDetection));
    }

    private static EmailSummaryDto NewEmail() => new(MessageId, "Invoice attached", "supplier@example.com", "Acme Ltd", DateTimeOffset.UtcNow);

    private static PdfAttachmentDto NewAttachment(string fileName = FileName) =>
        new(fileName, 1024, "application/pdf", System.Text.Encoding.UTF8.GetBytes($"pdf-bytes-for-{fileName}"));

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
