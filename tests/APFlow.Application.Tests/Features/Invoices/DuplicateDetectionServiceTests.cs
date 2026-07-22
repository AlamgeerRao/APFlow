using APFlow.Application.Features.Invoices;
using APFlow.Application.Interfaces;
using APFlow.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace APFlow.Application.Tests.Features.Invoices;

public class DuplicateDetectionServiceTests
{
    [Fact]
    public void Constructor_HasNoPersistenceDependency()
    {
        // WP-048's core requirement: DuplicateDetectionService must be a pure
        // compute service - no IInvoiceRepository, no DbContext, no SaveChangesAsync
        // access of any kind. Asserted via reflection against the actual constructor
        // (not just "current tests don't call a repository") so a future change that
        // reintroduces a persistence dependency fails this test immediately, rather
        // than relying on someone noticing during review.
        var constructor = Assert.Single(typeof(DuplicateDetectionService).GetConstructors());
        var parameterTypeNames = constructor.GetParameters().Select(p => p.ParameterType.Name).ToList();

        Assert.DoesNotContain(parameterTypeNames, name => name.Contains("Repository", StringComparison.Ordinal));
        Assert.DoesNotContain(parameterTypeNames, name => name.Contains("DbContext", StringComparison.Ordinal));
        Assert.DoesNotContain(parameterTypeNames, name => name.Contains("UnitOfWork", StringComparison.Ordinal));

        // The only dependency should be the logger - confirming there isn't some
        // other, differently-named persistence abstraction slipped in instead.
        var parameterType = Assert.Single(constructor.GetParameters()).ParameterType;
        Assert.Equal(typeof(Microsoft.Extensions.Logging.ILogger<DuplicateDetectionService>), parameterType);
    }

    [Fact]
    public void Check_NoOtherInvoices_ReturnsNotDuplicate()
    {
        var service = CreateService();
        var candidate = ExistingInvoice();

        var result = service.Check(candidate, []);

        Assert.False(result.IsPotentialDuplicate);
        Assert.Empty(result.Matches);
        Assert.Equal(candidate.Id, result.InvoiceId);
    }

    [Fact]
    public void Check_SupplierAndInvoiceNumberMatch_FlagsAsPotentialDuplicate_WithReasonRecorded()
    {
        var service = CreateService();
        var supplierId = Guid.NewGuid();
        var existing = ExistingInvoice(supplierId, "INV-100", new DateOnly(2026, 1, 15), 1200m);
        var candidate = ExistingInvoice(supplierId, "INV-100", new DateOnly(2026, 1, 15), 1200m);

        var result = service.Check(candidate, [existing]);

        Assert.True(result.IsPotentialDuplicate);
        var match = Assert.Single(result.Matches);
        Assert.Equal(existing.Id, match.MatchedInvoiceId);
        Assert.Equal(new[] { "Supplier", "InvoiceNumber" }, match.MatchedFields);
        Assert.Contains(existing.Id.ToString(), match.Reason);
        Assert.False(string.IsNullOrWhiteSpace(match.Reason));
    }

    [Fact]
    public void Check_SameSupplierAndInvoiceNumber_DifferentInvoiceDate_StillFlagged()
    {
        // WP-047: Invoice Date is not part of the matching criteria.
        var service = CreateService();
        var supplierId = Guid.NewGuid();
        var existing = ExistingInvoice(supplierId, "INV-100", new DateOnly(2026, 1, 15), 1200m);
        var candidate = ExistingInvoice(supplierId, "INV-100", new DateOnly(2026, 6, 30), 1200m);

        var result = service.Check(candidate, [existing]);

        Assert.True(result.IsPotentialDuplicate);
    }

    [Fact]
    public void Check_SameSupplierAndInvoiceNumber_DifferentGrossAmount_StillFlagged()
    {
        // WP-047: Gross Amount is not part of the matching criteria.
        var service = CreateService();
        var supplierId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 15);
        var existing = ExistingInvoice(supplierId, "INV-100", date, 1200m);
        var candidate = ExistingInvoice(supplierId, "INV-100", date, 999.99m);

        var result = service.Check(candidate, [existing]);

        Assert.True(result.IsPotentialDuplicate);
    }

    [Fact]
    public void Check_DifferentSupplier_NotFlagged()
    {
        var service = CreateService();
        var date = new DateOnly(2026, 1, 15);
        var existing = ExistingInvoice(Guid.NewGuid(), "INV-100", date, 1200m);
        var candidate = ExistingInvoice(Guid.NewGuid(), "INV-100", date, 1200m);

        var result = service.Check(candidate, [existing]);

        Assert.False(result.IsPotentialDuplicate);
    }

    [Fact]
    public void Check_DifferentInvoiceNumber_NotFlagged()
    {
        var service = CreateService();
        var supplierId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 15);
        var existing = ExistingInvoice(supplierId, "INV-100", date, 1200m);
        var candidate = ExistingInvoice(supplierId, "INV-200", date, 1200m);

        var result = service.Check(candidate, [existing]);

        Assert.False(result.IsPotentialDuplicate);
    }

    [Fact]
    public void Check_InvoiceNumberDiffersOnlyByCaseAndWhitespace_StillFlagged()
    {
        var service = CreateService();
        var supplierId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 15);
        var existing = ExistingInvoice(supplierId, "inv-100", date, 1200m);
        var candidate = ExistingInvoice(supplierId, "  INV-100  ", date, 1200m);

        var result = service.Check(candidate, [existing]);

        Assert.True(result.IsPotentialDuplicate);
    }

    [Fact]
    public void Check_DoesNotMatchAgainstItself()
    {
        var service = CreateService();
        var candidate = ExistingInvoice(Guid.NewGuid(), "INV-100", new DateOnly(2026, 1, 15), 1200m);

        // Candidate appears in its own comparison set - simulates a caller passing
        // the full invoice table (which naturally includes the candidate itself).
        var result = service.Check(candidate, [candidate]);

        Assert.False(result.IsPotentialDuplicate);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public void Check_CandidateMissingInvoiceNumber_SkipsCheck_ReturnsNotDuplicate()
    {
        var service = CreateService();
        var supplierId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 15);
        var existing = ExistingInvoice(supplierId, "INV-100", date, 1200m);
        var candidate = ExistingInvoice(supplierId, null, date, 1200m);

        var result = service.Check(candidate, [existing]);

        Assert.False(result.IsPotentialDuplicate);
        Assert.Empty(result.Matches);
    }

    [Fact]
    public void Check_CandidateMissingInvoiceDateAndGrossTotal_StillCompared()
    {
        // WP-047: only SupplierInvoiceNumber gates whether a comparison can be made
        // at all - InvoiceDate/GrossTotal being absent no longer skips the check.
        var service = CreateService();
        var supplierId = Guid.NewGuid();
        var existing = ExistingInvoice(supplierId, "INV-100", new DateOnly(2026, 1, 15), 1200m);
        var candidate = ExistingInvoice(supplierId, "INV-100", invoiceDate: null, grossTotal: null);

        var result = service.Check(candidate, [existing]);

        Assert.True(result.IsPotentialDuplicate);
    }

    [Fact]
    public void Check_ExistingInvoiceMissingComparisonField_NotMatchedAgainst()
    {
        var service = CreateService();
        var supplierId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 15);
        var existingWithGapInData = ExistingInvoice(supplierId, null, date, 1200m);
        var candidate = ExistingInvoice(supplierId, "INV-100", date, 1200m);

        var result = service.Check(candidate, [existingWithGapInData]);

        Assert.False(result.IsPotentialDuplicate);
    }

    [Fact]
    public void Check_MultipleExistingDuplicates_ReturnsAllMatches()
    {
        var service = CreateService();
        var supplierId = Guid.NewGuid();
        var date = new DateOnly(2026, 1, 15);
        var firstExisting = ExistingInvoice(supplierId, "INV-100", date, 1200m);
        var secondExisting = ExistingInvoice(supplierId, "INV-100", date, 1200m);
        var candidate = ExistingInvoice(supplierId, "INV-100", date, 1200m);

        var result = service.Check(candidate, [firstExisting, secondExisting]);

        Assert.True(result.IsPotentialDuplicate);
        Assert.Equal(2, result.Matches.Count);
    }

    [Fact]
    public void Check_NullCandidate_Throws()
    {
        var service = CreateService();

        Assert.Throws<ArgumentNullException>(() => service.Check(null!, []));
    }

    [Fact]
    public void Check_NullOtherInvoices_Throws()
    {
        var service = CreateService();
        var candidate = ExistingInvoice();

        Assert.Throws<ArgumentNullException>(() => service.Check(candidate, null!));
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

    private static DuplicateDetectionService CreateService() =>
        new(NullLogger<DuplicateDetectionService>.Instance);
}
