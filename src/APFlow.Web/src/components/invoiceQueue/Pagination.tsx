interface PaginationProps {
  page: number;
  pageSize: number;
  totalCount: number;
  onPageChange: (page: number) => void;
}

/** Prev/Next pagination with a "showing X–Y of Z" summary. */
export function Pagination({ page, pageSize, totalCount, onPageChange }: PaginationProps) {
  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const rangeStart = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const rangeEnd = Math.min(page * pageSize, totalCount);

  return (
    <div className="mt-4 flex items-center justify-between text-sm text-slate-600">
      <p>
        {totalCount === 0
          ? 'No results'
          : `Showing ${rangeStart}–${rangeEnd} of ${totalCount}`}
      </p>
      <div className="flex items-center gap-2">
        <button
          type="button"
          onClick={() => onPageChange(page - 1)}
          disabled={page <= 1}
          className="rounded-md border border-slate-200 px-3 py-1.5 font-medium text-ink-900 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
        >
          Previous
        </button>
        <span aria-live="polite">
          Page {page} of {totalPages}
        </span>
        <button
          type="button"
          onClick={() => onPageChange(page + 1)}
          disabled={page >= totalPages}
          className="rounded-md border border-slate-200 px-3 py-1.5 font-medium text-ink-900 hover:bg-slate-100 disabled:cursor-not-allowed disabled:opacity-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
        >
          Next
        </button>
      </div>
    </div>
  );
}
