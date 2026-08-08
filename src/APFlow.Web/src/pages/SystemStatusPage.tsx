import { PageHeading } from '@/components/layout/PageHeading';
import { useSystemStatus } from '@/api/useSystemStatus';
import { HealthReportCard } from '@/components/systemStatus/HealthReportCard';
import { SystemStatusLoadingState, SystemStatusErrorState } from '@/components/systemStatus/SystemStatusStates';

const BUILD_SHA = import.meta.env.VITE_BUILD_SHA;

/**
 * System Status (WP-043) — read-only operational status: liveness/readiness
 * (`/health/live`/`/health/ready`, WP-004/WP-024, unchanged — WP-043 only
 * added a JSON `ResponseWriter` so per-check detail is visible at all) and
 * the commit this build was compiled from (`VITE_BUILD_SHA`, injected by
 * `ci-cd.yml`'s existing build step).
 *
 * Originally scoped as an "Administration Portal" for user/role management
 * and application settings. Rescoped, per `docs/Sprint2-Plan.md`'s WP-043
 * entry: user/role management is retired from AP Flow's scope permanently
 * (identity/role assignment in the shared CIAM tenant is platform
 * infrastructure, not tenant-scoped application data — see WP-089's own
 * process, `docs/GB_Skips_Production_Migration_Notes.md` Item 3, which is now
 * the sole mechanism for role changes). Application/notification/accounting
 * settings remain genuinely open — no concrete setting exists yet to build a
 * screen around, so none is built speculatively.
 */
export function SystemStatusPage() {
  const status = useSystemStatus();

  return (
    <>
      <PageHeading title="System Status" description="Read-only operational status — no configuration is managed here." />

      {status.isLoading && <SystemStatusLoadingState />}

      {!status.isLoading && status.error && (
        <SystemStatusErrorState message={status.error} onRetry={status.retry} />
      )}

      {!status.isLoading && !status.error && (
        <div className="flex flex-col gap-4">
          <HealthReportCard
            title="Liveness"
            description="Is the API process itself up and responding."
            report={status.liveness}
          />
          <HealthReportCard
            title="Readiness"
            description="Can the API actually serve requests — database, mailbox, and storage dependencies."
            report={status.readiness}
          />

          <div className="rounded-md border border-slate-200 bg-white p-4">
            <h3 className="text-sm font-semibold text-ink-900">Build</h3>
            <p className="mt-1 text-sm text-slate-600" data-testid="build-sha">
              {BUILD_SHA ? BUILD_SHA.slice(0, 7) : 'Local development build'}
            </p>
          </div>
        </div>
      )}
    </>
  );
}
