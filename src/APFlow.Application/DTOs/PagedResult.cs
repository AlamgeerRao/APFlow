namespace APFlow.Application.DTOs;

/// <summary>
/// A single page of results from a filtered/sorted query, plus enough metadata for
/// a caller to render pagination controls without a second round-trip.
/// </summary>
/// <typeparam name="T">The item type for this page.</typeparam>
/// <param name="Items">The rows for this page, in the requested sort order.</param>
/// <param name="TotalCount">
/// The total number of rows matching the filter, across all pages - not just
/// <see cref="Items"/>.Count.
/// </param>
/// <param name="Page">The 1-based page number this result represents.</param>
/// <param name="PageSize">The page size used to produce this result.</param>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize)
{
    /// <summary>Total number of pages for <see cref="TotalCount"/> at <see cref="PageSize"/>. Zero when <see cref="TotalCount"/> is zero.</summary>
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
