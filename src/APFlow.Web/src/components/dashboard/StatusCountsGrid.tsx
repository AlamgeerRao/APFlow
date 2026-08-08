import { Link } from 'react-router-dom';
import type { FolderSummary } from '@/types/supplierFolder';
import { statusCodeToQueuePath } from '@/components/layout/navConfig';

interface StatusCountsGridProps {
  folderCounts: FolderSummary[];
}

/**
 * Per-status invoice count tiles (WP-030), built from the same
 * `FolderSummary[]` the Suppliers page's `FolderList` already renders
 * (`supplierFolderClient.getFolderCounts`, WP-059) — no separate data
 * source. Each tile links to that status's Invoice Queue view, via the
 * same route `navConfig.buildInvoiceQueueLinks` builds for the nav's own
 * sub-links, so a tile and its matching nav entry always agree.
 */
export function StatusCountsGrid({ folderCounts }: StatusCountsGridProps) {
  if (folderCounts.length === 0) {
    return (
      <div className="rounded-md border border-slate-200 bg-white p-8 text-center text-sm text-slate-600">
        No active invoices in any folder right now.
      </div>
    );
  }

  return (
    <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4" data-testid="status-counts-grid">
      {folderCounts.map((folder) => (
        <Link
          key={folder.statusCode}
          to={statusCodeToQueuePath(folder.statusCode)}
          className="flex flex-col gap-1 rounded-md border border-slate-200 bg-white p-4 transition-colors hover:border-accent-600 hover:bg-slate-50 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
        >
          <span className="text-2xl font-semibold text-ink-900">{folder.count}</span>
          <span className="text-sm text-slate-600">{folder.statusLabel}</span>
        </Link>
      ))}
    </div>
  );
}
