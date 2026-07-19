using APFlow.Integrations.Graph;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace APFlow.Integrations.Tests.Graph;

public class PdfExtractionServiceTests
{
    // Real PDF file signature bytes, so tests exercise HasPdfSignature honestly
    // rather than happening to pass despite it.
    private static readonly byte[] ValidPdfBytes = "%PDF-1.4\n%some fake content"u8.ToArray();
    private static readonly byte[] NotActuallyPdfBytes = "This is not a PDF, just text."u8.ToArray();

    [Fact]
    public async Task ExtractPdfAttachmentsAsync_EmptyMessageId_ReturnsFailure()
    {
        var (service, _) = CreateService();

        var result = await service.ExtractPdfAttachmentsAsync("");

        Assert.True(result.IsFailure);
        Assert.Equal("PdfExtraction.InvalidMessageId", result.Error.Code);
    }

    [Fact]
    public async Task ExtractPdfAttachmentsAsync_MailboxNotConfigured_ReturnsFailure()
    {
        var (service, _) = CreateService(mailboxUpn: "");

        var result = await service.ExtractPdfAttachmentsAsync("msg-1");

        Assert.True(result.IsFailure);
        Assert.Equal("PdfExtraction.MailboxNotConfigured", result.Error.Code);
    }

    [Fact]
    public async Task ExtractPdfAttachmentsAsync_NoAttachments_ReturnsSuccessWithEmptyList()
    {
        var (service, _) = CreateService();

        var result = await service.ExtractPdfAttachmentsAsync("msg-1");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ExtractPdfAttachmentsAsync_PdfByContentType_WithValidSignature_IsExtracted()
    {
        var (service, ops) = CreateService();
        ops.Attachments = [FileAttachment("invoice", "application/pdf", ValidPdfBytes)];

        var result = await service.ExtractPdfAttachmentsAsync("msg-1");

        Assert.True(result.IsSuccess);
        var extracted = Assert.Single(result.Value);
        Assert.Equal("invoice", extracted.FileName);
        Assert.Equal("application/pdf", extracted.ContentType);
        Assert.Equal(ValidPdfBytes, extracted.Content);
    }

    [Fact]
    public async Task ExtractPdfAttachmentsAsync_PdfByFileExtension_GenericContentType_WithValidSignature_IsExtracted()
    {
        // A PDF sent with a generic/incorrect content-type but a correct .pdf
        // extension is still accepted - see IsPdf's doc comment for why.
        var (service, ops) = CreateService();
        ops.Attachments = [FileAttachment("invoice.pdf", "application/octet-stream", ValidPdfBytes)];

        var result = await service.ExtractPdfAttachmentsAsync("msg-1");

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value);
    }

    [Fact]
    public async Task ExtractPdfAttachmentsAsync_ClaimsPdf_ButSignatureDoesNotMatch_IsSkipped()
    {
        // The core Recommendation 1 test: content-type and filename both say PDF,
        // but the actual bytes don't start with "%PDF-". Both claims are
        // sender-controlled, untrusted input - this proves they aren't trusted alone.
        var (service, ops) = CreateService();
        ops.Attachments = [FileAttachment("invoice.pdf", "application/pdf", NotActuallyPdfBytes)];

        var result = await service.ExtractPdfAttachmentsAsync("msg-1");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ExtractPdfAttachmentsAsync_ClaimsPdf_ContentTooShortForSignature_IsSkipped_NoException()
    {
        // Edge case: content shorter than the 5-byte signature must not throw.
        var (service, ops) = CreateService();
        ops.Attachments = [FileAttachment("invoice.pdf", "application/pdf", [0x25, 0x50])];

        var result = await service.ExtractPdfAttachmentsAsync("msg-1");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ExtractPdfAttachmentsAsync_InlineAttachment_IsSkipped_EvenIfPdf()
    {
        var (service, ops) = CreateService();
        ops.Attachments = [FileAttachment("logo.pdf", "application/pdf", ValidPdfBytes, isInline: true)];

        var result = await service.ExtractPdfAttachmentsAsync("msg-1");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Theory]
    [InlineData("photo.jpg", "image/jpeg")]
    [InlineData("document.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("data.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    public async Task ExtractPdfAttachmentsAsync_NonPdfAttachment_IsSkipped(string fileName, string contentType)
    {
        var (service, ops) = CreateService();
        ops.Attachments = [FileAttachment(fileName, contentType, [1, 2, 3])];

        var result = await service.ExtractPdfAttachmentsAsync("msg-1");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ExtractPdfAttachmentsAsync_NonFileAttachment_LookingLikePdf_IsSkipped()
    {
        // e.g. a OneDrive reference/link attachment named "invoice.pdf" with no bytes.
        var (service, ops) = CreateService();
        ops.Attachments = [new GraphAttachmentInfo("invoice.pdf", 0, "application/pdf", IsInline: false, IsFileAttachment: false, Content: null)];

        var result = await service.ExtractPdfAttachmentsAsync("msg-1");

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value);
    }

    [Fact]
    public async Task ExtractPdfAttachmentsAsync_MixedAttachments_ExtractsOnlyValidPdfs()
    {
        var (service, ops) = CreateService();
        ops.Attachments =
        [
            FileAttachment("invoice1.pdf", "application/pdf", ValidPdfBytes),
            FileAttachment("logo.png", "image/png", [2], isInline: true),
            FileAttachment("readme.txt", "text/plain", [3]),
            FileAttachment("fake.pdf", "application/pdf", NotActuallyPdfBytes),
            FileAttachment("invoice2.pdf", "application/pdf", ValidPdfBytes),
        ];

        var result = await service.ExtractPdfAttachmentsAsync("msg-1");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count);
        Assert.Contains(result.Value, a => a.FileName == "invoice1.pdf");
        Assert.Contains(result.Value, a => a.FileName == "invoice2.pdf");
        Assert.DoesNotContain(result.Value, a => a.FileName == "fake.pdf");
    }

    [Fact]
    public async Task ExtractPdfAttachmentsAsync_SizeAndContentTypePreserved()
    {
        var (service, ops) = CreateService();
        ops.Attachments = [new GraphAttachmentInfo("invoice.pdf", 12345, "application/pdf", IsInline: false, IsFileAttachment: true, Content: ValidPdfBytes)];

        var result = await service.ExtractPdfAttachmentsAsync("msg-1");

        var extracted = Assert.Single(result.Value);
        Assert.Equal(12345, extracted.SizeInBytes);
        Assert.Equal("application/pdf", extracted.ContentType);
    }

    [Fact]
    public async Task ExtractPdfAttachmentsAsync_GraphFails_ReturnsFailure_DoesNotPropagate()
    {
        var (service, ops) = CreateService();
        ops.Mode = FakeGraphAttachmentOperations.Behavior.ThrowGeneric;

        var result = await service.ExtractPdfAttachmentsAsync("msg-1");

        Assert.True(result.IsFailure);
        Assert.Equal("PdfExtraction.ExtractionFailed", result.Error.Code);
    }

    [Fact]
    public async Task ExtractPdfAttachmentsAsync_CallerCancels_PropagatesCancellation()
    {
        var (service, ops) = CreateService();
        ops.Mode = FakeGraphAttachmentOperations.Behavior.ThrowOperationCanceled;
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.ExtractPdfAttachmentsAsync("msg-1", cts.Token));
    }

    private static GraphAttachmentInfo FileAttachment(string fileName, string contentType, byte[] content, bool isInline = false) =>
        new(fileName, content.Length, contentType, IsInline: isInline, IsFileAttachment: true, Content: content);

    private static (PdfExtractionService Service, FakeGraphAttachmentOperations Operations) CreateService(string mailboxUpn = "ap-invoices@example.com")
    {
        var operations = new FakeGraphAttachmentOperations();
        var options = Options.Create(new GraphOptions
        {
            TenantId = "fake-tenant",
            ClientId = "fake-client",
            MailboxUserPrincipalName = mailboxUpn,
        });

        var service = new PdfExtractionService(operations, options, NullLogger<PdfExtractionService>.Instance);
        return (service, operations);
    }
}
