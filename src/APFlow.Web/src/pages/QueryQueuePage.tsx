import { PageHeading } from '@/components/layout/PageHeading';
import { useInvoiceQueue } from '@/api/useInvoiceQueue';
import { InvoiceQueueFilters } from '@/components/invoiceQueue/InvoiceQueueFilters';
import { InvoiceQueueTable } from '@/components/invoiceQueue/InvoiceQueueTable';
import { Pagination } from '@/components/invoiceQueue/Pagination';
import { InvoiceQueueLoadingState, InvoiceQueueErrorState } from '@/components/invoiceQueue/InvoiceQueueLoadingState';

/**
 * Invoices with an open query to a supplier (WP-074) - NEEDS_QUERY,
 * QUERY_RAISED, and AWAITING_SUPPLIER_RESPONSE combined into one view, via
 * the real `GET /api/invoices?statuses=...` multi-status filter (WP-011's
 * query service already supported adding this additively - see
 * InvoiceQueryParameters.Statuses - no new endpoint was needed). Reuses the
 * Invoice Queue's own table, pagination, loading/error states, and search
 * box exactly as they are - this is not a separate implementation, just a
 * fixed status set fed into the same `useInvoiceQueue` hook every other
 * queue view already uses. No status dropdown: the view is already scoped
 * to these three statuses, so a filter offering every other status wouldn't
 * make sense here.
 */
const QUERY_QUEUE_STATUSES = ['NEEDS_QUERY', 'QUERY_RAISED', 'AWAITING_SUPPLIER_RESPONSE'];

export function QueryQueuePage() {
  const queue = useInvoiceQueue(undefined, QUERY_QUEUE_STATUSES);

  return (
    <>
      <PageHeading title="Query Queue" description="Invoices with an open query to a supplier." />

      <InvoiceQueueFilters
        search={queue.search}
        onSearchChange={queue.setSearch}
        status={queue.status}
        onStatusChange={queue.setStatus}
        hideStatusFilter
      />

      {queue.isLoading && <InvoiceQueueLoadingState />}

      {!queue.isLoading && queue.error && (
        <InvoiceQueueErrorState message={queue.error} onRetry={queue.retry} />
      )}

      {!queue.isLoading && !queue.error && queue.result && (
        <>
          <InvoiceQueueTable
            invoices={queue.result.items}
            sortBy={queue.sortBy}
            sortDirection={queue.sortDirection}
            onSortChange={queue.toggleSort}
          />
          <Pagination
            page={queue.result.page}
            pageSize={queue.result.pageSize}
            totalCount={queue.result.totalCount}
            onPageChange={queue.setPage}
          />
        </>
      )}
    </>
  );
}
