import type { HealthReport } from '@/types/systemStatus';
import { HealthStatusBadge } from '@/components/systemStatus/HealthStatusBadge';

interface HealthReportCardProps {
  title: string;
  description: string;
  report: HealthReport | null;
}

/**
 * One health probe's card (Liveness or Readiness, WP-043) — an overall
 * status badge, plus one row per dependency check when the report carries
 * any (`/health/live` always reports zero checks by design, see
 * `ApiServiceCollectionExtensions.UseApiHealthChecks`'s own doc comment).
 */
export function HealthReportCard({ title, description, report }: HealthReportCardProps) {
  return (
    <div className="rounded-md border border-slate-200 bg-white p-4">
      <div className="flex items-center justify-between gap-3">
        <div>
          <h3 className="text-sm font-semibold text-ink-900">{title}</h3>
          <p className="text-xs text-slate-600">{description}</p>
        </div>
        {report && <HealthStatusBadge status={report.status} />}
      </div>

      {report && report.checks.length > 0 && (
        <ul className="mt-3 divide-y divide-slate-100 border-t border-slate-100">
          {report.checks.map((check) => (
            <li key={check.name} className="flex items-center justify-between gap-3 py-2 text-sm">
              <div className="min-w-0">
                <p className="text-ink-900">{check.name}</p>
                {check.description && <p className="truncate text-xs text-slate-500">{check.description}</p>}
              </div>
              <HealthStatusBadge status={check.status} />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
