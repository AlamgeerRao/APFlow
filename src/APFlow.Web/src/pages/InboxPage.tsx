import { PageHeading } from '@/components/layout/PageHeading';
import { useIngestionIssues } from '@/api/useIngestionIssues';
import { IngestionIssuesTable } from '@/components/ingestion/IngestionIssuesTable';
import { Pagination } from '@/components/invoiceQueue/Pagination';

/** Inbox (WP-076): emails the ingestion pipeline could not turn into an invoice, because no processable PDF attachment was found. */
export function InboxPage() {
  const inbox = useIngestionIssues();

  return (
    <>
      <PageHeading title="Inbox" description="Emails that arrived with no processable invoice attachment." />

      {inbox.isLoading && (
        <div
          className="flex items-center justify-center rounded-md border border-slate-200 bg-white p-8 text-sm text-slate-600"
          role="status"
        >
          Loading Inbox…
        </div>
      )}

      {!inbox.isLoading && inbox.error && (
        <div className="flex flex-col items-center gap-3 rounded-md border border-red-200 bg-red-50 p-8 text-center" role="alert">
          <p className="text-sm font-medium text-red-800">{inbox.error}</p>
          <button
            type="button"
            onClick={inbox.retry}
            className="rounded-md border border-red-300 bg-white px-3 py-1.5 text-sm font-medium text-red-800 hover:bg-red-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
          >
            Retry
          </button>
        </div>
      )}

      {!inbox.isLoading && !inbox.error && inbox.result && (
        <>
          <IngestionIssuesTable issues={inbox.result.items} />
          <Pagination
            page={inbox.result.page}
            pageSize={inbox.result.pageSize}
            totalCount={inbox.result.totalCount}
            onPageChange={inbox.setPage}
          />
        </>
      )}
    </>
  );
}
