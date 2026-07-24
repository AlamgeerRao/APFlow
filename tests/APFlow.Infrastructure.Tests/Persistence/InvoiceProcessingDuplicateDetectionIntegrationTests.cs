using APFlow.Application.DTOs;
using APFlow.Application.Features.Approval;
using APFlow.Application.Features.Audit;
using APFlow.Application.Features.Invoices;
using APFlow.Application.Features.Workflow;
using APFlow.Application.Features.Suppliers;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Entities;
using APFlow.Domain.Common.Constants;
using APFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace APFlow.Infrastructure.Tests.Persistence;

/// <summary>
/// WP-049's required integration test: two invoices ingested through the real
/// pipeline with identical Supplier + Invoice Number - the second is flagged and
/// the flag is genuinely persisted, proven via a real (InMemory-provider)
/// AppDbContext, not a hand-written fake. Every Application-layer collaborator
/// (InvoiceService, SupplierService, DuplicateDetectionService, AuditService,
/// InvoiceProcessingService) is real, backed by the same AppDbContext - only the
/// four interfaces that wrap genuinely external SDKs (Graph, Blob Storage,
/// Document Intelligence) are faked, matching the same boundary this project draws
/// everywhere else (see WP-012's own test design notes).
/// </summary>
public class InvoiceProcessingDuplicateDetectionIntegrationTests
{
    [Fact]
    public async Task ProcessUnreadEmailsAsync_TwoInvoicesSameSupplierAndInvoiceNumber_SecondFlaggedAndPersisted()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);

        var invoiceRepository = new InvoiceRepository(context);
        var supplierRepository = new SupplierRepository(context);
        var auditLogRepository = new AuditLogRepository(context);
        var auditService = new AuditService(auditLogRepository, NullLogger<AuditService>.Instance);
        var approvalAuthorizationService = new ApprovalAuthorizationService(new ApprovalPolicyRepository(context));
        var invoiceService = new InvoiceService(
            invoiceRepository, supplierRepository, auditService, new FakeCurrentUserService(tenantId), approvalAuthorizationService,
            new WorkflowValidationService(new WorkflowTemplateRepository(context)), NullLogger<InvoiceService>.Instance);
        var supplierService = new SupplierService(supplierRepository, NullLogger<SupplierService>.Instance);
        var duplicateDetectionService = new DuplicateDetectionService(NullLogger<DuplicateDetectionService>.Instance);

        var emailSync = new FakeEmailSyncService();
        var pdfExtraction = new FakePdfExtractionService();
        var blobStorage = new FakeBlobStorageService();
        var documentAnalysis = new FakeDocumentAnalysisService();

        const string supplierName = "Acme Ltd";
        const string invoiceNumber = "INV-100";

        emailSync.UnreadEmails.Add(new EmailSummaryDto("msg-1", "Invoice 1", "supplier@example.com", supplierName, DateTimeOffset.UtcNow));
        emailSync.UnreadEmails.Add(new EmailSummaryDto("msg-2", "Invoice 2", "supplier@example.com", supplierName, DateTimeOffset.UtcNow));
        pdfExtraction.AttachmentsByMessageId["msg-1"] = [new PdfAttachmentDto("invoice1.pdf", 1024, "application/pdf", [1, 2, 3])];
        pdfExtraction.AttachmentsByMessageId["msg-2"] = [new PdfAttachmentDto("invoice2.pdf", 1024, "application/pdf", [4, 5, 6])];
        documentAnalysis.ResultsByFileContent[1] = NewExtraction(supplierName, invoiceNumber);
        documentAnalysis.ResultsByFileContent[4] = NewExtraction(supplierName, invoiceNumber);

        var service = new InvoiceProcessingService(
            emailSync, pdfExtraction, blobStorage, documentAnalysis, duplicateDetectionService,
            invoiceService, supplierService, invoiceRepository, NullLogger<InvoiceProcessingService>.Instance);

        var result = await service.ProcessUnreadEmailsAsync();

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Items.Count);
        Assert.All(result.Value.Items, item => Assert.Equal(InvoiceProcessingOutcome.Processed, item.Outcome));

        var firstItem = result.Value.Items[0];
        var secondItem = result.Value.Items[1];
        Assert.False(firstItem.IsPotentialDuplicate);
        Assert.True(secondItem.IsPotentialDuplicate);

        // The real proof: read back from the database via a brand-new query (not
        // the tracked in-memory instances the pipeline just created), confirming
        // the flag was genuinely committed, not just set on an object in memory.
        var persistedInvoices = await context.Invoices
            .AsNoTracking()
            .OrderBy(i => i.CreatedAtUtc)
            .ToListAsync();

        Assert.Equal(2, persistedInvoices.Count);
        Assert.False(persistedInvoices[0].IsPotentialDuplicate);
        Assert.Null(persistedInvoices[0].DuplicateCheckReason);
        Assert.True(persistedInvoices[1].IsPotentialDuplicate);
        Assert.False(string.IsNullOrWhiteSpace(persistedInvoices[1].DuplicateCheckReason));
        Assert.Contains("Supplier and Invoice Number", persistedInvoices[1].DuplicateCheckReason);

        // Both invoices land directly at Extracted (WP-049: no separate Received-then-
        // advance step, since that would require its own, separate commit).
        Assert.All(persistedInvoices, i => Assert.Equal(InvoiceStatusCodes.Extracted, i.Status));
    }

    [Fact]
    public async Task ProcessUnreadEmailsAsync_DifferentInvoiceNumbers_NeitherFlagged()
    {
        var tenantId = Guid.NewGuid();
        using var context = CreateContext(tenantId);

        var invoiceRepository = new InvoiceRepository(context);
        var supplierRepository = new SupplierRepository(context);
        var auditLogRepository = new AuditLogRepository(context);
        var auditService = new AuditService(auditLogRepository, NullLogger<AuditService>.Instance);
        var approvalAuthorizationService = new ApprovalAuthorizationService(new ApprovalPolicyRepository(context));
        var invoiceService = new InvoiceService(
            invoiceRepository, supplierRepository, auditService, new FakeCurrentUserService(tenantId), approvalAuthorizationService,
            new WorkflowValidationService(new WorkflowTemplateRepository(context)), NullLogger<InvoiceService>.Instance);
        var supplierService = new SupplierService(supplierRepository, NullLogger<SupplierService>.Instance);
        var duplicateDetectionService = new DuplicateDetectionService(NullLogger<DuplicateDetectionService>.Instance);

        var emailSync = new FakeEmailSyncService();
        var pdfExtraction = new FakePdfExtractionService();
        var blobStorage = new FakeBlobStorageService();
        var documentAnalysis = new FakeDocumentAnalysisService();

        const string supplierName = "Acme Ltd";

        emailSync.UnreadEmails.Add(new EmailSummaryDto("msg-1", "Invoice 1", "supplier@example.com", supplierName, DateTimeOffset.UtcNow));
        emailSync.UnreadEmails.Add(new EmailSummaryDto("msg-2", "Invoice 2", "supplier@example.com", supplierName, DateTimeOffset.UtcNow));
        pdfExtraction.AttachmentsByMessageId["msg-1"] = [new PdfAttachmentDto("invoice1.pdf", 1024, "application/pdf", [1, 2, 3])];
        pdfExtraction.AttachmentsByMessageId["msg-2"] = [new PdfAttachmentDto("invoice2.pdf", 1024, "application/pdf", [4, 5, 6])];
        documentAnalysis.ResultsByFileContent[1] = NewExtraction(supplierName, "INV-100");
        documentAnalysis.ResultsByFileContent[4] = NewExtraction(supplierName, "INV-200");

        var service = new InvoiceProcessingService(
            emailSync, pdfExtraction, blobStorage, documentAnalysis, duplicateDetectionService,
            invoiceService, supplierService, invoiceRepository, NullLogger<InvoiceProcessingService>.Instance);

        var result = await service.ProcessUnreadEmailsAsync();

        Assert.True(result.IsSuccess);
        Assert.All(result.Value.Items, item => Assert.False(item.IsPotentialDuplicate));

        var persistedInvoices = await context.Invoices.AsNoTracking().ToListAsync();
        Assert.All(persistedInvoices, i => Assert.False(i.IsPotentialDuplicate));
    }

    private static InvoiceExtractionResult NewExtraction(string supplierName, string invoiceNumber) => new(
        SupplierName: new ExtractedField<string?>(supplierName, 0.95),
        SupplierInvoiceNumber: new ExtractedField<string?>(invoiceNumber, 0.95),
        InvoiceDate: new ExtractedField<DateOnly?>(new DateOnly(2026, 1, 1), 0.95),
        DueDate: new ExtractedField<DateOnly?>(new DateOnly(2026, 2, 1), 0.95),
        Currency: "GBP",
        NetAmount: new ExtractedField<decimal?>(100m, 0.95),
        Vat: new ExtractedField<decimal?>(20m, 0.95),
        GrossTotal: new ExtractedField<decimal?>(120m, 0.95));

    private static AppDbContext CreateContext(Guid tenantId) =>
        new(
            new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,
            new FakeCurrentUserService(tenantId));

    private sealed class FakeCurrentUserService : ICurrentUserService
    {
        public FakeCurrentUserService(Guid tenantId)
        {
            TenantId = tenantId.ToString();
        }

        public bool IsAuthenticated => true;
        public string? UserId => "test-user";
        public string? Email => null;
        public string? TenantId { get; }
        public IReadOnlyCollection<string> Roles => [];
        public bool IsInRole(string role) => false;
    }

    // Minimal local fakes for the four genuinely-external interfaces. Duplicated
    // rather than shared with APFlow.Application.Tests' equivalents (which are
    // `internal` and not visible across assemblies) - same pattern this project
    // already uses for FakeCurrentUserService in every Infrastructure.Tests file.

    private sealed class FakeEmailSyncService : IEmailSyncService
    {
        public List<EmailSummaryDto> UnreadEmails { get; } = [];
        public List<string> MarkedAsProcessedMessageIds { get; } = [];

        public Task<Result<IReadOnlyList<EmailSummaryDto>>> SyncUnreadEmailsAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success<IReadOnlyList<EmailSummaryDto>>(UnreadEmails));

        public Task<Result> MarkAsProcessedAsync(string messageId, CancellationToken cancellationToken = default)
        {
            MarkedAsProcessedMessageIds.Add(messageId);
            return Task.FromResult(Result.Success());
        }
    }

    private sealed class FakePdfExtractionService : IPdfExtractionService
    {
        public Dictionary<string, List<PdfAttachmentDto>> AttachmentsByMessageId { get; } = [];

        public Task<Result<IReadOnlyList<PdfAttachmentDto>>> ExtractPdfAttachmentsAsync(string messageId, CancellationToken cancellationToken = default)
        {
            var attachments = AttachmentsByMessageId.TryGetValue(messageId, out var list)
                ? (IReadOnlyList<PdfAttachmentDto>)list
                : [];
            return Task.FromResult(Result.Success(attachments));
        }
    }

    private sealed class FakeBlobStorageService : IBlobStorageService
    {
        public Task<Result<string>> UploadAsync(string blobName, Stream content, string? contentType = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success($"https://fake.blob.core.windows.net/container/{blobName}"));

        public Task<Result<Stream>> DownloadAsync(string blobName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result> DeleteAsync(string blobName, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<Result<Uri>> GenerateSasUrlAsync(string blobName, TimeSpan validFor, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<bool> VerifyContainerAccessAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    /// <summary>Keys results by the first byte of the PDF content, letting each test attachment map to a distinct extraction result.</summary>
    private sealed class FakeDocumentAnalysisService : IDocumentAnalysisService
    {
        public Dictionary<byte, InvoiceExtractionResult> ResultsByFileContent { get; } = [];

        public Task<Result<InvoiceExtractionResult>> AnalyzeInvoiceAsync(byte[] pdfContent, CancellationToken cancellationToken = default) =>
            Task.FromResult(Result.Success(ResultsByFileContent[pdfContent[0]]));
    }
}
