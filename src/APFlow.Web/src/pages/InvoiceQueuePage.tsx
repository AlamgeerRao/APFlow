import { useParams } from 'react-router-dom';
import { PageHeading } from '@/components/layout/PageHeading';
import { useWorkflowTemplate } from '@/api/useWorkflowTemplate';

/**
 * Placeholder route content for both /invoices (all statuses) and
 * /invoices/:statusCode (a single workflow-status queue). Business
 * functionality (listing/filtering invoices) is out of scope for WP-014 —
 * this only confirms the data-driven nav routes resolve to a real page.
 */
export function InvoiceQueuePage() {
  const { statusCode } = useParams<{ statusCode?: string }>();
  const { template } = useWorkflowTemplate();

  const matchedStatus = template?.statuses.find(
    (status) => status.code.toLowerCase().replace(/_/g, '-') === statusCode,
  );

  const title = statusCode ? matchedStatus?.name ?? statusCode : 'Invoice Queue';

  return (
    <>
      <PageHeading
        title={title}
        description={
          statusCode
            ? `Invoices currently in the "${title}" status.`
            : 'All invoices across every workflow status for this tenant.'
        }
      />
      <p className="text-sm text-slate-600">Invoice list content will be implemented in WP-015.</p>
    </>
  );
}
