import type { AuditEntry } from '@/types/invoiceDetail';

interface AuditSummaryPanelProps {
  entries: AuditEntry[];
}

function formatTimestamp(iso: string): string {
  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(new Date(iso));
}

/** Audit/activity history summary for the invoice (WP-016 task 7, WP-013 data). */
export function AuditSummaryPanel({ entries }: AuditSummaryPanelProps) {
  return (
    <section aria-labelledby="audit-summary-heading" className="rounded-md border border-slate-200 bg-white p-4">
      <h2 id="audit-summary-heading" className="mb-3 text-sm font-semibold text-ink-900">
        Audit Summary
      </h2>
      {entries.length === 0 ? (
        <p className="text-sm text-slate-600">No audit history available for this invoice.</p>
      ) : (
        <ol className="space-y-3">
          {entries.map((entry) => (
            <li key={entry.id} className="border-l-2 border-slate-200 pl-3">
              <p className="text-sm font-medium text-ink-900">{entry.action}</p>
              <p className="text-sm text-slate-600">{entry.description}</p>
              <p className="mt-0.5 text-xs text-slate-400">
                {formatTimestamp(entry.timestamp)} · {entry.actor}
              </p>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
