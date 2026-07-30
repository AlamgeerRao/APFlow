using APFlow.Application.DTOs;
using APFlow.Application.Interfaces;
using APFlow.Domain.Common;

namespace APFlow.Application.Tests.Features;

/// <summary>
/// Hand-written fake, same pattern as every fake elsewhere in this codebase. Uses a
/// factory (not a static result) since callers like
/// <c>SupplierFolderQueryService</c> issue several differently-parameterised
/// searches (one per status for folder counts; repeated paged calls for
/// exhaustive fetches) and tests need to respond differently per call.
/// </summary>
internal sealed class FakeInvoiceQueryService : IInvoiceQueryService
{
    public Func<InvoiceQueryParameters, Result<PagedResult<InvoiceListItemDto>>>? ResultFactory { get; set; }
    public List<InvoiceQueryParameters> Calls { get; } = [];

    public Task<Result<PagedResult<InvoiceListItemDto>>> SearchAsync(
        InvoiceQueryParameters parameters, CancellationToken cancellationToken = default)
    {
        Calls.Add(parameters);
        var result = ResultFactory?.Invoke(parameters)
            ?? Result.Success(new PagedResult<InvoiceListItemDto>([], 0, parameters.Page, parameters.PageSize));
        return Task.FromResult(result);
    }
}
