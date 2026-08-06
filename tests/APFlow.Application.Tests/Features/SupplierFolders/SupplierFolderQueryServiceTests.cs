using APFlow.Application.DTOs;
using APFlow.Application.Features.SupplierFolders;
using APFlow.Application.Tests.Features;
using APFlow.Domain.Common;
using Xunit;

namespace APFlow.Application.Tests.Features.SupplierFolders;

public class SupplierFolderQueryServiceTests
{
    [Fact]
    public async Task GetFolderCountsAsync_ReturnsOneEntryPerNonTerminalStatus_OrderedBySortOrder_TerminalExcluded()
    {
        var workflowQuery = new FakeWorkflowQueryService
        {
            TemplateToReturn = NewTemplate(
                (Code: "AWAITING_REVIEW", Name: "Awaiting Review", IsTerminal: false, SortOrder: 40),
                (Code: "NEEDS_QUERY", Name: "Needs Query", IsTerminal: false, SortOrder: 10),
                (Code: "ARCHIVED", Name: "Archived", IsTerminal: true, SortOrder: 130)),
        };
        var invoiceQuery = new FakeInvoiceQueryService
        {
            ResultFactory = parameters => Result.Success(new PagedResult<InvoiceListItemDto>(
                [], parameters.Status == "NEEDS_QUERY" ? 3 : 7, 1, 1)),
        };
        var service = new SupplierFolderQueryService(workflowQuery, invoiceQuery);

        var result = await service.GetFolderCountsAsync(search: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Count); // ARCHIVED (terminal) excluded
        Assert.Equal("NEEDS_QUERY", result.Value[0].StatusCode); // SortOrder 10 first
        Assert.Equal(3, result.Value[0].Count);
        Assert.Equal("AWAITING_REVIEW", result.Value[1].StatusCode); // SortOrder 40 second
        Assert.Equal(7, result.Value[1].Count);
    }

    [Fact]
    public async Task GetFolderCountsAsync_ExcludesReceivedProcessingExtracted_PermanentlyUnreachableSinceWP071()
    {
        var workflowQuery = new FakeWorkflowQueryService
        {
            TemplateToReturn = NewTemplate(
                (Code: "RECEIVED", Name: "Received", IsTerminal: false, SortOrder: 10),
                (Code: "PROCESSING", Name: "Processing", IsTerminal: false, SortOrder: 20),
                (Code: "EXTRACTED", Name: "Extracted", IsTerminal: false, SortOrder: 30),
                (Code: "AWAITING_REVIEW", Name: "Awaiting Review", IsTerminal: false, SortOrder: 40)),
        };
        var invoiceQuery = new FakeInvoiceQueryService
        {
            ResultFactory = parameters => Result.Success(new PagedResult<InvoiceListItemDto>([], 0, 1, 1)),
        };
        var service = new SupplierFolderQueryService(workflowQuery, invoiceQuery);

        var result = await service.GetFolderCountsAsync(search: null);

        Assert.True(result.IsSuccess);
        Assert.Equal("AWAITING_REVIEW", Assert.Single(result.Value).StatusCode);
    }

    [Fact]
    public async Task GetFolderCountsAsync_PassesSearchThroughToEachStatusQuery()
    {
        var workflowQuery = new FakeWorkflowQueryService
        {
            TemplateToReturn = NewTemplate((Code: "NEEDS_QUERY", Name: "Needs Query", IsTerminal: false, SortOrder: 10)),
        };
        var invoiceQuery = new FakeInvoiceQueryService();
        var service = new SupplierFolderQueryService(workflowQuery, invoiceQuery);

        await service.GetFolderCountsAsync(search: "Acme");

        Assert.Equal("Acme", Assert.Single(invoiceQuery.Calls).Search);
        Assert.Equal(1, invoiceQuery.Calls[0].PageSize); // only TotalCount is needed - no rows fetched
    }

    [Fact]
    public async Task GetFolderCountsAsync_TemplateFetchFails_PropagatesFailure()
    {
        var workflowQuery = new FakeWorkflowQueryService { FailureToReturn = new Error("Workflow.TemplateNotFound", "boom") };
        var service = new SupplierFolderQueryService(workflowQuery, new FakeInvoiceQueryService());

        var result = await service.GetFolderCountsAsync(search: null);

        Assert.True(result.IsFailure);
        Assert.Equal("Workflow.TemplateNotFound", result.Error.Code);
    }

    [Fact]
    public async Task GetFolderCountsAsync_PerStatusQueryFails_PropagatesFailure()
    {
        var workflowQuery = new FakeWorkflowQueryService
        {
            TemplateToReturn = NewTemplate((Code: "NEEDS_QUERY", Name: "Needs Query", IsTerminal: false, SortOrder: 10)),
        };
        var invoiceQuery = new FakeInvoiceQueryService
        {
            ResultFactory = _ => Result.Failure<PagedResult<InvoiceListItemDto>>(new Error("InvoiceQuery.InvalidPage", "boom")),
        };
        var service = new SupplierFolderQueryService(workflowQuery, invoiceQuery);

        var result = await service.GetFolderCountsAsync(search: null);

        Assert.True(result.IsFailure);
        Assert.Equal("InvoiceQuery.InvalidPage", result.Error.Code);
    }

    [Fact]
    public async Task GetSupplierNamesAsync_ReturnsDistinctNames_SortedAlphabetically()
    {
        var invoiceQuery = new FakeInvoiceQueryService
        {
            ResultFactory = parameters => Result.Success(new PagedResult<InvoiceListItemDto>(
                [
                    NewInvoiceItem("Zeta Ltd"),
                    NewInvoiceItem("Acme Ltd"),
                    NewInvoiceItem("Acme Ltd"), // duplicate supplier across two invoices
                ],
                3, parameters.Page, parameters.PageSize)),
        };
        var service = new SupplierFolderQueryService(new FakeWorkflowQueryService(), invoiceQuery);

        var result = await service.GetSupplierNamesAsync(folder: null, search: null);

        Assert.True(result.IsSuccess);
        Assert.Equal(["Acme Ltd", "Zeta Ltd"], result.Value);
    }

    [Fact]
    public async Task GetSupplierNamesAsync_AggregatesAcrossMultipleInternalPages_NoSupplierMissed()
    {
        // Correctness proof: a supplier whose only invoice happens to sort past
        // the first internal fetch page must NOT be silently dropped.
        var invoiceQuery = new FakeInvoiceQueryService
        {
            ResultFactory = parameters => parameters.Page switch
            {
                1 => Result.Success(new PagedResult<InvoiceListItemDto>(
                    Enumerable.Range(0, InvoiceQueryParameters.MaxPageSize).Select(i => NewInvoiceItem($"Supplier {i}")).ToList(),
                    TotalCount: InvoiceQueryParameters.MaxPageSize + 1, parameters.Page, parameters.PageSize)),
                2 => Result.Success(new PagedResult<InvoiceListItemDto>(
                    [NewInvoiceItem("Last Page Supplier")],
                    TotalCount: InvoiceQueryParameters.MaxPageSize + 1, parameters.Page, parameters.PageSize)),
                _ => throw new InvalidOperationException("Unexpected third page requested."),
            },
        };
        var service = new SupplierFolderQueryService(new FakeWorkflowQueryService(), invoiceQuery);

        var result = await service.GetSupplierNamesAsync(folder: null, search: null);

        Assert.True(result.IsSuccess);
        Assert.Contains("Last Page Supplier", result.Value);
        Assert.Equal(InvoiceQueryParameters.MaxPageSize + 1, result.Value.Count);
        Assert.Equal(2, invoiceQuery.Calls.Count); // exactly two internal page fetches, not one and not three
    }

    [Fact]
    public async Task GetGroupedInvoicesAsync_GroupsBySupplier_SortsGroupsAlphabetically_InvoicesNewestFirstWithinGroup()
    {
        var invoiceQuery = new FakeInvoiceQueryService
        {
            ResultFactory = parameters => Result.Success(new PagedResult<InvoiceListItemDto>(
                [
                    NewInvoiceItem("Zeta Ltd", new DateOnly(2026, 1, 1)),
                    NewInvoiceItem("Acme Ltd", new DateOnly(2026, 1, 1)),
                    NewInvoiceItem("Acme Ltd", new DateOnly(2026, 6, 1)),
                ],
                3, parameters.Page, parameters.PageSize)),
        };
        var service = new SupplierFolderQueryService(new FakeWorkflowQueryService(), invoiceQuery);

        var result = await service.GetGroupedInvoicesAsync(new SupplierFolderQueryParameters());

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.TotalSuppliers);
        Assert.Equal("Acme Ltd", result.Value.Groups[0].SupplierName); // alphabetically first
        Assert.Equal(2, result.Value.Groups[0].Count);
        Assert.Equal(new DateOnly(2026, 6, 1), result.Value.Groups[0].Invoices[0].InvoiceDate); // newest first within group
        Assert.Equal("Zeta Ltd", result.Value.Groups[1].SupplierName);
    }

    [Fact]
    public async Task GetGroupedInvoicesAsync_SupplierFilter_NarrowsToOneGroup()
    {
        var invoiceQuery = new FakeInvoiceQueryService
        {
            ResultFactory = parameters => Result.Success(new PagedResult<InvoiceListItemDto>(
                [NewInvoiceItem("Zeta Ltd"), NewInvoiceItem("Acme Ltd")], 2, parameters.Page, parameters.PageSize)),
        };
        var service = new SupplierFolderQueryService(new FakeWorkflowQueryService(), invoiceQuery);

        var result = await service.GetGroupedInvoicesAsync(new SupplierFolderQueryParameters(Supplier: "Acme Ltd"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalSuppliers);
        Assert.Equal("Acme Ltd", Assert.Single(result.Value.Groups).SupplierName);
    }

    [Fact]
    public async Task GetGroupedInvoicesAsync_PaginatesOverGroups_NotIndividualInvoices()
    {
        var invoiceQuery = new FakeInvoiceQueryService
        {
            ResultFactory = parameters => Result.Success(new PagedResult<InvoiceListItemDto>(
                [
                    NewInvoiceItem("Alpha"), NewInvoiceItem("Alpha"), NewInvoiceItem("Alpha"), // one group, 3 invoices
                    NewInvoiceItem("Beta"),
                    NewInvoiceItem("Gamma"),
                ],
                5, parameters.Page, parameters.PageSize)),
        };
        var service = new SupplierFolderQueryService(new FakeWorkflowQueryService(), invoiceQuery);

        var result = await service.GetGroupedInvoicesAsync(new SupplierFolderQueryParameters(Page: 1, PageSize: 2));

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Value.TotalSuppliers); // 3 distinct suppliers overall
        Assert.Equal(2, result.Value.Groups.Count); // page size 2 GROUPS, despite Alpha alone having 3 invoices
        Assert.Equal("Alpha", result.Value.Groups[0].SupplierName);
        Assert.Equal(3, result.Value.Groups[0].Invoices.Count); // Alpha's group is NOT truncated mid-list
    }

    [Fact]
    public async Task GetGroupedInvoicesAsync_GroupSpanningInternalFetchPageBoundary_StaysWhole()
    {
        // Correctness proof: the same supplier appearing in both internal fetch
        // pages must be merged into ONE group, not split into two or have one
        // half silently dropped.
        var invoiceQuery = new FakeInvoiceQueryService
        {
            ResultFactory = parameters => parameters.Page switch
            {
                1 => Result.Success(new PagedResult<InvoiceListItemDto>(
                    Enumerable.Range(0, InvoiceQueryParameters.MaxPageSize - 1).Select(_ => NewInvoiceItem("Acme Ltd")).ToList(),
                    TotalCount: InvoiceQueryParameters.MaxPageSize + 1, parameters.Page, parameters.PageSize)),
                2 => Result.Success(new PagedResult<InvoiceListItemDto>(
                    [NewInvoiceItem("Acme Ltd"), NewInvoiceItem("Acme Ltd")],
                    TotalCount: InvoiceQueryParameters.MaxPageSize + 1, parameters.Page, parameters.PageSize)),
                _ => throw new InvalidOperationException("Unexpected third page requested."),
            },
        };
        var service = new SupplierFolderQueryService(new FakeWorkflowQueryService(), invoiceQuery);

        var result = await service.GetGroupedInvoicesAsync(new SupplierFolderQueryParameters());

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.TotalSuppliers); // still one supplier, not two
        Assert.Equal(InvoiceQueryParameters.MaxPageSize + 1, Assert.Single(result.Value.Groups).Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetGroupedInvoicesAsync_InvalidPage_ReturnsFailure(int page)
    {
        var service = new SupplierFolderQueryService(new FakeWorkflowQueryService(), new FakeInvoiceQueryService());

        var result = await service.GetGroupedInvoicesAsync(new SupplierFolderQueryParameters(Page: page));

        Assert.True(result.IsFailure);
        Assert.Equal("SupplierFolderQuery.InvalidPage", result.Error.Code);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public async Task GetGroupedInvoicesAsync_InvalidPageSize_ReturnsFailure(int pageSize)
    {
        var service = new SupplierFolderQueryService(new FakeWorkflowQueryService(), new FakeInvoiceQueryService());

        var result = await service.GetGroupedInvoicesAsync(new SupplierFolderQueryParameters(PageSize: pageSize));

        Assert.True(result.IsFailure);
        Assert.Equal("SupplierFolderQuery.InvalidPageSize", result.Error.Code);
    }

    private static WorkflowTemplateDto NewTemplate(params (string Code, string Name, bool IsTerminal, int SortOrder)[] statuses) => new(
        Id: Guid.NewGuid(),
        DomainName: "Invoice",
        Name: "Test Template",
        IsTenantSpecific: false,
        Statuses: statuses.Select(s => new WorkflowStatusDto(s.Code, s.Name, s.IsTerminal, s.SortOrder)).ToList(),
        Transitions: []);

    private static InvoiceListItemDto NewInvoiceItem(string supplierName, DateOnly? invoiceDate = null) => new(
        Id: Guid.NewGuid(),
        SupplierId: Guid.NewGuid(),
        SupplierName: supplierName,
        SupplierInvoiceNumber: "INV-1",
        InvoiceDate: invoiceDate ?? new DateOnly(2026, 1, 1),
        DueDate: null,
        Currency: "GBP",
        GrossTotal: 120m,
        Status: "AWAITING_REVIEW",
        CreatedAtUtc: DateTimeOffset.UtcNow,
        IsPotentialDuplicate: false,
        DuplicateCheckReason: null,
        DuplicateMatchInvoiceId: null);
}
