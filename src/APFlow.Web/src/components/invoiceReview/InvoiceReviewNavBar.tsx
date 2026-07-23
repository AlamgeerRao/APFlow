import { Link } from 'react-router-dom';

interface InvoiceReviewNavBarProps {
  previousId: string | null;
  nextId: string | null;
  position: number | null;
  total: number | null;
}

function reviewPath(id: string): string {
  return `/invoices/review/${id}`;
}

/** Previous/Next invoice navigation for the Review Screen (WP-016 task 6). */
export function InvoiceReviewNavBar({ previousId, nextId, position, total }: InvoiceReviewNavBarProps) {
  return (
    <div className="mb-4 flex items-center justify-between">
      {previousId ? (
        <Link
          to={reviewPath(previousId)}
          className="rounded-md border border-slate-200 bg-white px-3 py-1.5 text-sm font-medium text-ink-900 hover:bg-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
        >
          ← Previous
        </Link>
      ) : (
        <span
          aria-disabled="true"
          className="cursor-not-allowed rounded-md border border-slate-200 px-3 py-1.5 text-sm font-medium text-slate-400"
        >
          ← Previous
        </span>
      )}

      {position !== null && total !== null && (
        <span className="text-sm text-slate-600" aria-live="polite">
          {position} of {total}
        </span>
      )}

      {nextId ? (
        <Link
          to={reviewPath(nextId)}
          className="rounded-md border border-slate-200 bg-white px-3 py-1.5 text-sm font-medium text-ink-900 hover:bg-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
        >
          Next →
        </Link>
      ) : (
        <span
          aria-disabled="true"
          className="cursor-not-allowed rounded-md border border-slate-200 px-3 py-1.5 text-sm font-medium text-slate-400"
        >
          Next →
        </span>
      )}
    </div>
  );
}
