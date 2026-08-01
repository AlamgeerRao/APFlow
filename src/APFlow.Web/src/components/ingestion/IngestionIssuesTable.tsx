import type { IngestionIssue } from '@/types/ingestionIssue';
import { formatDate } from '@/utils/format';

interface IngestionIssuesTableProps {
  issues: IngestionIssue[];
}

/**
 * Narrow, read-only Inbox listing (WP-076): sender, subject, first-seen
 * date, and occurrence count where greater than 1 - exactly the WP's own
 * column list, no status/actions/filters, since none were asked for.
 */
export function IngestionIssuesTable({ issues }: IngestionIssuesTableProps) {
  if (issues.length === 0) {
    return (
      <div className="rounded-md border border-slate-200 bg-white p-8 text-center text-sm text-slate-600">
        No flagged emails.
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-md border border-slate-200 bg-white">
      <table className="min-w-full divide-y divide-slate-200 text-sm">
        <thead>
          <tr>
            <th scope="col" className="px-4 py-3 text-left font-medium text-slate-600">Sender</th>
            <th scope="col" className="px-4 py-3 text-left font-medium text-slate-600">Subject</th>
            <th scope="col" className="px-4 py-3 text-left font-medium text-slate-600">First Seen</th>
            <th scope="col" className="px-4 py-3 text-left font-medium text-slate-600">
              <span className="sr-only">Occurrence count</span>
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {issues.map((issue) => (
            <tr key={issue.id} data-testid="ingestion-issue-row">
              <td className="px-4 py-3 text-ink-900">{issue.senderName ?? issue.senderAddress}</td>
              <td className="px-4 py-3 text-ink-900">{issue.subject}</td>
              <td className="px-4 py-3 text-ink-900">{formatDate(issue.firstSeenUtc.slice(0, 10))}</td>
              <td className="px-4 py-3 text-ink-900">
                {issue.occurrenceCount > 1 && (
                  <span
                    className="inline-flex items-center rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-medium text-amber-800"
                    title={`Seen ${issue.occurrenceCount} times in this conversation`}
                  >
                    ×{issue.occurrenceCount}
                  </span>
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
