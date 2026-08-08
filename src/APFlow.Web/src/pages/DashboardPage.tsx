import { PageHeading } from '@/components/layout/PageHeading';
import { useDashboard } from '@/api/useDashboard';
import { StatusCountsGrid } from '@/components/dashboard/StatusCountsGrid';
import { RecentActivityList } from '@/components/dashboard/RecentActivityList';
import { DashboardLoadingState, DashboardErrorState } from '@/components/dashboard/DashboardStates';

/**
 * Dashboard (WP-030): replaces the WP-014 placeholder with real status
 * counts and recent activity. Status counts reuse
 * `supplierFolderClient.getFolderCounts` (WP-059, the same data the
 * Suppliers page's folder list already renders); recent activity reuses
 * `GET /api/invoices` sorted by `CreatedAtUtc` descending — no new backend
 * endpoint for either, per `docs/Sprint2-Plan.md` §3 WP-030's own
 * confirmation. See `useDashboard`'s doc comment for the data-fetch shape.
 */
export function DashboardPage() {
  const dashboard = useDashboard();

  return (
    <>
      <PageHeading title="Dashboard" description="Overview of invoice processing activity." />

      {dashboard.isLoading && <DashboardLoadingState />}

      {!dashboard.isLoading && dashboard.error && (
        <DashboardErrorState message={dashboard.error} onRetry={dashboard.retry} />
      )}

      {!dashboard.isLoading && !dashboard.error && (
        <div className="flex flex-col gap-8">
          <section>
            <h2 className="mb-3 text-sm font-semibold text-ink-900">Invoices by status</h2>
            <StatusCountsGrid folderCounts={dashboard.folderCounts} />
          </section>

          <section>
            <h2 className="mb-3 text-sm font-semibold text-ink-900">Recent activity</h2>
            <RecentActivityList items={dashboard.recentActivity} />
          </section>
        </div>
      )}
    </>
  );
}
