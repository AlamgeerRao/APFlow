import { Link } from 'react-router-dom';
import type { RecentActivityItem } from '@/types/dashboard';
import { formatDateTime } from '@/utils/format';
import { InvoiceStatusBadge } from '@/components/invoiceQueue/InvoiceStatusBadge';

interface RecentActivityListProps {
  items: RecentActivityItem[];
}

/**
 * Most-recently-created invoices (WP-030), sourced from the same
 * `GET /api/invoices` endpoint the Invoice Queue uses (sorted by
 * `CreatedAtUtc` descending) — no new endpoint. Each row links to the
 * Review Screen, matching `InvoiceQueueTable`'s own row-click convention.
 */
export function RecentActivityList({ items }: RecentActivityListProps) {
  if (items.length === 0) {
    return (
      <div className="rounded-md border border-slate-200 bg-white p-8 text-center text-sm text-slate-600">
        No recent invoice activity.
      </div>
    );
  }

  return (
    <ul className="divide-y divide-slate-100 rounded-md border border-slate-200 bg-white">
      {items.map((item) => (
        <li key={item.id}>
          <Link
            to={`/invoices/review/${item.id}`}
            className="flex items-center justify-between gap-4 px-4 py-3 hover:bg-slate-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
          >
            <div className="min-w-0">
              <p className="truncate text-sm font-medium text-ink-900">{item.supplierName ?? '—'}</p>
              <p className="truncate text-xs text-slate-600">{item.invoiceNumber ?? '—'}</p>
            </div>
            <div className="flex shrink-0 items-center gap-3">
              <span className="text-xs text-slate-500">{formatDateTime(item.createdAtUtc)}</span>
              <InvoiceStatusBadge statusCode={item.status} />
            </div>
          </Link>
        </li>
      ))}
    </ul>
  );
}
