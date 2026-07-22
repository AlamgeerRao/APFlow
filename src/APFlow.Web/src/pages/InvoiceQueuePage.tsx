import { useParams } from 'react-router-dom';
import { PageHeading } from '@/components/layout/PageHeading';
import { useWorkflowTemplate } from '@/api/useWorkflowTemplate';
import { useInvoiceQueue } from '@/api/useInvoiceQueue';
import { InvoiceQueueFilters } from '@/components/invoiceQueue/InvoiceQueueFilters';
import { InvoiceQueueTable } from '@/components/invoiceQueue/InvoiceQueueTable';
import { Pagination } from '@/components/invoiceQueue/Pagination';
import { InvoiceQueueLoadingState, InvoiceQueueErrorState } from '@/components/invoiceQueue/InvoiceQueueLoadingState';

/**
 * Invoice work queue (WP-015). Read-only: no approval, editing, or notes
 * actions live here. Serves both /invoices (every non-terminal status)
 * and /invoices/:statusCode (a single workflow-status queue, reached via
 * WP-014's data-driven nav sub-links).
 */
export function InvoiceQueuePage() {
  const { statusCode } = useParams<{ statusCode?: string }>();
  const { template } = useWorkflowTemplate();

  // The route param is the kebab-case slug WP-014's nav derives from each
  // status code (see navConfig.ts) — resolve it back to the real
  // StatusReference.code the query layer expects.
  const matchedStatus = template?.statuses.find(
    (status) => status.code.toLowerCase().replace(/_/g, '-') === statusCode,
  );
  const initialStatus = statusCode ? matchedStatus?.code : undefined;

  const queue = useInvoiceQueue(initialStatus);

  const title = statusCode ? matchedStatus?.name ?? statusCode : 'Invoice Queue';
  const description = statusCode
    ? `Invoices currently in the "${title}" status.`
    : 'All invoices across every workflow status for this tenant.';

  return (
    <>
      <PageHeading title={title} description={description} />

      <InvoiceQueueFilters
        search={queue.search}
        onSearchChange={queue.setSearch}
        status={queue.status}
        onStatusChange={queue.setStatus}
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
