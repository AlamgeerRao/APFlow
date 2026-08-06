using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;
using APFlow.Domain.Common.Constants;

namespace APFlow.Application.Features.SupplierFolders;

/// <summary>
/// Default implementation of <see cref="ISupplierFolderQueryService"/>. Depends
/// only on <see cref="IWorkflowQueryService"/> and <see cref="IInvoiceQueryService"/>
/// (both plain, EF-Core-free interfaces), so this class is fully unit-testable
/// with fakes of those two - no database, no EF Core provider required.
/// </summary>
public sealed class SupplierFolderQueryService : ISupplierFolderQueryService
{
    /// <summary>
    /// WP-085 follow-up: since WP-071, every invoice is created directly at
    /// AWAITING_REVIEW, so these three statuses are permanently unreachable in
    /// normal operation - the folder chip always shows a zero count. WP-085
    /// already excluded them from the Invoice Queue nav
    /// (navConfig.ts's NON_ACTIONABLE_NAV_STATUS_CODES) but missed this
    /// endpoint, which independently feeds the Suppliers page's folder list.
    /// They remain valid statuses in the data model, template, and transition
    /// graph; only this summary's rendering is filtered, by status code so
    /// this doesn't silently break if the template's ordering ever changes.
    /// </summary>
    private static readonly HashSet<string> NonActionableFolderStatusCodes =
    [
        InvoiceStatusCodes.Received,
        InvoiceStatusCodes.Processing,
        InvoiceStatusCodes.Extracted,
    ];

    private readonly IWorkflowQueryService _workflowQueryService;
    private readonly IInvoiceQueryService _invoiceQueryService;

    /// <summary>Creates a new <see cref="SupplierFolderQueryService"/>.</summary>
    public SupplierFolderQueryService(IWorkflowQueryService workflowQueryService, IInvoiceQueryService invoiceQueryService)
    {
        _workflowQueryService = workflowQueryService;
        _invoiceQueryService = invoiceQueryService;
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<FolderSummaryDto>>> GetFolderCountsAsync(
        string? search, CancellationToken cancellationToken = default)
    {
        var templateResult = await _workflowQueryService.GetActiveTemplateAsync(WorkflowDomains.Invoice, cancellationToken);
        if (templateResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<FolderSummaryDto>>(templateResult.Error);
        }

        var nonTerminalStatuses = templateResult.Value.Statuses
            .Where(s => !s.IsTerminal && !NonActionableFolderStatusCodes.Contains(s.Code))
            .OrderBy(s => s.SortOrder)
            .ToList();

        var summaries = new List<FolderSummaryDto>(nonTerminalStatuses.Count);

        foreach (var status in nonTerminalStatuses)
        {
            // PageSize: 1 - only PagedResult.TotalCount is needed here, not the
            // rows themselves, so this asks for the smallest page the API allows
            // rather than fetching (and discarding) actual invoice data per
            // status. One request per non-terminal status (currently 11) - a
            // reasoned simple default reusing the existing, already-tested
            // SearchAsync, not a new single GROUP BY repository query. See
            // docs/WP-059-Supplier-Folder-Cors-Doc-Decisions.md.
            var countResult = await _invoiceQueryService.SearchAsync(
                new InvoiceQueryParameters(Status: status.Code, Search: search, Page: 1, PageSize: 1),
                cancellationToken);

            if (countResult.IsFailure)
            {
                return Result.Failure<IReadOnlyList<FolderSummaryDto>>(countResult.Error);
            }

            summaries.Add(new FolderSummaryDto(status.Code, status.Name, countResult.Value.TotalCount));
        }

        return Result.Success<IReadOnlyList<FolderSummaryDto>>(summaries);
    }

    /// <inheritdoc />
    public async Task<Result<IReadOnlyList<string>>> GetSupplierNamesAsync(
        string? folder, string? search, CancellationToken cancellationToken = default)
    {
        var invoicesResult = await FetchAllMatchingInvoicesAsync(folder, search, cancellationToken);
        if (invoicesResult.IsFailure)
        {
            return Result.Failure<IReadOnlyList<string>>(invoicesResult.Error);
        }

        var names = invoicesResult.Value
            .Select(i => i.SupplierName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        return Result.Success<IReadOnlyList<string>>(names);
    }

    /// <inheritdoc />
    public async Task<Result<SupplierGroupedInvoicesDto>> GetGroupedInvoicesAsync(
        SupplierFolderQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        if (parameters.Page < 1)
        {
            return Result.Failure<SupplierGroupedInvoicesDto>(
                new Error("SupplierFolderQuery.InvalidPage", "Page must be 1 or greater."));
        }

        if (parameters.PageSize < 1 || parameters.PageSize > SupplierFolderQueryParameters.MaxPageSize)
        {
            return Result.Failure<SupplierGroupedInvoicesDto>(new Error(
                "SupplierFolderQuery.InvalidPageSize",
                $"PageSize must be between 1 and {SupplierFolderQueryParameters.MaxPageSize}."));
        }

        var invoicesResult = await FetchAllMatchingInvoicesAsync(parameters.Folder, parameters.Search, cancellationToken);
        if (invoicesResult.IsFailure)
        {
            return Result.Failure<SupplierGroupedInvoicesDto>(invoicesResult.Error);
        }

        var matching = invoicesResult.Value;

        // The supplier filter is applied here, in memory, rather than pushed down
        // as a new IInvoiceQueryService parameter: it filters by supplier NAME (a
        // free-text string, matching the frontend's actual need - narrowing an
        // already-grouped view to one group), whereas InvoiceQueryParameters only
        // supports a SupplierId (Guid) filter. Introducing a name-based filter
        // parameter on the shared invoice-search contract, just for this one
        // caller, would be a bigger change than this task's own "no service-layer
        // changes to IInvoiceQueryService" spirit calls for.
        if (!string.IsNullOrWhiteSpace(parameters.Supplier))
        {
            matching = matching
                .Where(i => string.Equals(i.SupplierName, parameters.Supplier, StringComparison.Ordinal))
                .ToList();
        }

        var allGroups = matching
            .GroupBy(i => i.SupplierName ?? string.Empty, StringComparer.Ordinal)
            .Select(g => new SupplierGroupDto(
                g.Key,
                g.Count(),
                g.OrderByDescending(i => i.InvoiceDate).ToList()))
            .OrderBy(g => g.SupplierName, StringComparer.Ordinal)
            .ToList();

        var totalSuppliers = allGroups.Count;
        var pagedGroups = allGroups
            .Skip((parameters.Page - 1) * parameters.PageSize)
            .Take(parameters.PageSize)
            .ToList();

        return Result.Success(new SupplierGroupedInvoicesDto(pagedGroups, totalSuppliers, parameters.Page, parameters.PageSize));
    }

    /// <summary>
    /// Fetches every invoice matching <paramref name="folder"/>/<paramref name="search"/>,
    /// looping across pages internally at <see cref="InvoiceQueryParameters.MaxPageSize"/>
    /// increments rather than capping at a single page. This is a correctness
    /// requirement, not a style choice: both callers (supplier-name listing and
    /// grouping) need the COMPLETE matching set - capping at one page could
    /// silently drop an entire supplier's group (if its invoices happen to sort
    /// past the cap) or omit a supplier from the filter dropdown entirely,
    /// which is a data-correctness bug, not just a missed optimisation. See
    /// docs/WP-059-Supplier-Folder-Cors-Doc-Decisions.md.
    /// </summary>
    private async Task<Result<List<InvoiceListItemDto>>> FetchAllMatchingInvoicesAsync(
        string? folder, string? search, CancellationToken cancellationToken)
    {
        var allItems = new List<InvoiceListItemDto>();
        var page = 1;

        while (true)
        {
            var pageResult = await _invoiceQueryService.SearchAsync(
                new InvoiceQueryParameters(Status: folder, Search: search, Page: page, PageSize: InvoiceQueryParameters.MaxPageSize),
                cancellationToken);

            if (pageResult.IsFailure)
            {
                return Result.Failure<List<InvoiceListItemDto>>(pageResult.Error);
            }

            allItems.AddRange(pageResult.Value.Items);

            if (pageResult.Value.Items.Count == 0 || allItems.Count >= pageResult.Value.TotalCount)
            {
                break;
            }

            page++;
        }

        return Result.Success(allItems);
    }
}
