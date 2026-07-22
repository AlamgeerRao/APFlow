using APFlow.Application.Features.Invoices;
using APFlow.Application.Tests.Features;
using APFlow.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace APFlow.Application.Tests.Features.Invoices;

public class DuplicateDetectionServiceTests
{
    [Fact]
    public async Task CheckAsync_UnknownInvoice_ReturnsFailure()
    {
        var (service, _) = CreateService();

        var result = await service.CheckAsync(Guid.NewGuid());

        Assert.True(result.IsFailure);
        Assert.Equal("DuplicateDetection.InvoiceNotFound", result.Error.Code);
    }

    [Fact]
    public async Task CheckAsync_NoOtherInvoices_ReturnsNotDuplicate()
    {
        var (service, repository) = CreateService();
        var candidate = ExistingInvoice();
        repository.Invoices.Add(candidate);

        var result = await service.CheckAsync(candidate.Id);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsPotentialDuplicate);
        Assert.Empty(result.Value.Matches);
    }

    [Fact]
    public async Task CheckAsync_SupplierAndInvoiceNumberMatch_FlagsAsPotentialDuplicate_WithReasonRecorded()
    {
        var (service, repository) = CreateService();
        var supplierId = Guid.NewGuid();
        var existing = ExistingInvoice(supplierId, "INV-100", new DateOnly(2026, 1, 15), 1200m);
        var candidate = ExistingInvoice(supplierId, "INV-100", new DateOnly(2026, 1, 15), 1200m);
        repository.Invoices.Add(existing);
        repository.Invoices.Add(candidate);

        var result = await service.CheckAsync(candidate.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsPotentialDuplicate);
        var match = Assert.Single(result.Value.Matches);
        Assert.Equal(existing.Id, match.MatchedInvoiceId);
        Assert.Equal(new[] { "Supplier", "InvoiceNumber" }, match.MatchedFields);
        Assert.Contains(existing.Id.ToString(), match.Reason);
        Assert.False(string.IsNullOrWhiteSpace(match.Reason));
    }

    [Fact]
    public async Task CheckAsync_SameSupplierAndInvoiceNumber_DifferentInvoiceDate_StillFlagged()
    {
        // WP-047: Invoice Date is no longer part of the matching criteria - proves
        // the removed four-field rule's date component genuinely has no effect.
        var (service, repository) = CreateService();
        var supplierId = Guid.NewGuid();
        var existing = ExistingInvoice(supplierId, "INV-100", new DateOnly(2026, 1, 15), 1200m);
        var candidate = ExistingInvoice(supplierId, "INV-100", new DateOnly(2026, 6, 30), 1200m);
        repository.Invoices.Add(existing);
        repository.Invoices.Add(candidate);

        var result = await service.CheckAsync(candidate.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsPotentialDuplicate);
    }

    [Fact]
    public async Task CheckAsync_SameSupplierAndInvoiceNumber_DifferentGrossAmount_StillFlagged()
    {
        // WP-047: Gross Amount is no longer part of the matching criteria - proves
        // the removed four-field rule's amount component genuinely has no effect.
        var (service, repository) = CreateService();
        var supplierId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 15);
        var existing = ExistingInvoice(supplierId, "INV-100", date, 1200m);
        var candidate = ExistingInvoice(supplierId, "INV-100", date, 999.99m);
        repository.Invoices.Add(existing);
        repository.Invoices.Add(candidate);

        var result = await service.CheckAsync(candidate.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsPotentialDuplicate);
    }

    [Fact]
    public async Task CheckAsync_DifferentSupplier_NotFlagged()
    {
        var (service, repository) = CreateService();
        var date = new DateOnly(2026, 1, 15);
        var existing = ExistingInvoice(Guid.NewGuid(), "INV-100", date, 1200m);
        var candidate = ExistingInvoice(Guid.NewGuid(), "INV-100", date, 1200m);
        repository.Invoices.Add(existing);
        repository.Invoices.Add(candidate);

        var result = await service.CheckAsync(candidate.Id);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsPotentialDuplicate);
    }

    [Fact]
    public async Task CheckAsync_DifferentInvoiceNumber_NotFlagged()
    {
        var (service, repository) = CreateService();
        var supplierId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 15);
        var existing = ExistingInvoice(supplierId, "INV-100", date, 1200m);
        var candidate = ExistingInvoice(supplierId, "INV-200", date, 1200m);
        repository.Invoices.Add(existing);
        repository.Invoices.Add(candidate);

        var result = await service.CheckAsync(candidate.Id);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsPotentialDuplicate);
    }

    [Fact]
    public async Task CheckAsync_InvoiceNumberDiffersOnlyByCaseAndWhitespace_StillFlagged()
    {
        var (service, repository) = CreateService();
        var supplierId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 15);
        var existing = ExistingInvoice(supplierId, "inv-100", date, 1200m);
        var candidate = ExistingInvoice(supplierId, "  INV-100  ", date, 1200m);
        repository.Invoices.Add(existing);
        repository.Invoices.Add(candidate);

        var result = await service.CheckAsync(candidate.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsPotentialDuplicate);
    }

    [Fact]
    public async Task CheckAsync_DoesNotMatchAgainstItself()
    {
        var (service, repository) = CreateService();
        var candidate = ExistingInvoice(Guid.NewGuid(), "INV-100", new DateOnly(2026, 1, 15), 1200m);
        repository.Invoices.Add(candidate);

        var result = await service.CheckAsync(candidate.Id);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsPotentialDuplicate);
        Assert.Empty(result.Value.Matches);
    }

    [Fact]
    public async Task CheckAsync_CandidateMissingInvoiceNumber_SkipsCheck_ReturnsNotDuplicate()
    {
        var (service, repository) = CreateService();
        var supplierId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 15);
        var existing = ExistingInvoice(supplierId, "INV-100", date, 1200m);
        var candidate = ExistingInvoice(supplierId, null, date, 1200m);
        repository.Invoices.Add(existing);
        repository.Invoices.Add(candidate);

        var result = await service.CheckAsync(candidate.Id);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsPotentialDuplicate);
        Assert.Empty(result.Value.Matches);
    }

    [Fact]
    public async Task CheckAsync_CandidateMissingInvoiceDateAndGrossTotal_StillCompared()
    {
        // WP-047: only SupplierInvoiceNumber gates whether a comparison can be made
        // at all now - InvoiceDate/GrossTotal being absent no longer skips the check.
        var (service, repository) = CreateService();
        var supplierId = Guid.NewGuid();
        var existing = ExistingInvoice(supplierId, "INV-100", new DateOnly(2026, 1, 15), 1200m);
        var candidate = ExistingInvoice(supplierId, "INV-100", invoiceDate: null, grossTotal: null);
        repository.Invoices.Add(existing);
        repository.Invoices.Add(candidate);

        var result = await service.CheckAsync(candidate.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsPotentialDuplicate);
    }

    [Fact]
    public async Task CheckAsync_ExistingInvoiceMissingComparisonField_NotMatchedAgainst()
    {
        var (service, repository) = CreateService();
        var supplierId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 15);
        var existingWithGapInData = ExistingInvoice(supplierId, null, date, 1200m);
        var candidate = ExistingInvoice(supplierId, "INV-100", date, 1200m);
        repository.Invoices.Add(existingWithGapInData);
        repository.Invoices.Add(candidate);

        var result = await service.CheckAsync(candidate.Id);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.IsPotentialDuplicate);
    }

    [Fact]
    public async Task CheckAsync_MultipleExistingDuplicates_ReturnsAllMatches()
    {
        var (service, repository) = CreateService();
        var supplierId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 15);
        var firstExisting = ExistingInvoice(supplierId, "INV-100", date, 1200m);
        var secondExisting = ExistingInvoice(supplierId, "INV-100", date, 1200m);
        var candidate = ExistingInvoice(supplierId, "INV-100", date, 1200m);
        repository.Invoices.Add(firstExisting);
        repository.Invoices.Add(secondExisting);
        repository.Invoices.Add(candidate);

        var result = await service.CheckAsync(candidate.Id);

        Assert.True(result.IsSuccess);
        Assert.True(result.Value.IsPotentialDuplicate);
        Assert.Equal(2, result.Value.Matches.Count);
    }

    [Fact]
    public async Task CheckAsync_DoesNotModifyInvoiceOrCallSaveChanges()
    {
        var (service, repository) = CreateService();
        var supplierId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 15);
        var existing = ExistingInvoice(supplierId, "INV-100", date, 1200m);
        var candidate = ExistingInvoice(supplierId, "INV-100", date, 1200m);
        repository.Invoices.Add(existing);
        repository.Invoices.Add(candidate);

        await service.CheckAsync(candidate.Id);

        Assert.False(repository.SaveChangesCalled);
        Assert.Equal(APFlow.Domain.Enums.InvoiceStatus.Received, candidate.Status);
    }

    private static Invoice ExistingInvoice(
        Guid? supplierId = null,
        string? supplierInvoiceNumber = "INV-1",
        DateOnly? invoiceDate = null,
        decimal? grossTotal = 100m) => new()
    {
        SupplierId = supplierId ?? Guid.NewGuid(),
        SupplierInvoiceNumber = supplierInvoiceNumber,
        InvoiceDate = invoiceDate ?? new DateOnly(2026, 1, 1),
        GrossTotal = grossTotal,
    };

    private static (DuplicateDetectionService Service, FakeInvoiceRepository Repository) CreateService()
    {
        var repository = new FakeInvoiceRepository();
        var service = new DuplicateDetectionService(repository, NullLogger<DuplicateDetectionService>.Instance);
        return (service, repository);
    }
}
