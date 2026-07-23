import { useParams } from 'react-router-dom';
import { PageHeading } from '@/components/layout/PageHeading';
import { useInvoiceDetail } from '@/api/useInvoiceDetail';
import { useInvoiceNavigation } from '@/api/useInvoiceNavigation';
import { InvoiceReviewNavBar } from '@/components/invoiceReview/InvoiceReviewNavBar';
import { InvoiceHeaderSummary } from '@/components/invoiceReview/InvoiceHeaderSummary';
import { DuplicateWarningBanner } from '@/components/invoiceReview/DuplicateWarningBanner';
import { ExtractedFieldsPanel } from '@/components/invoiceReview/ExtractedFieldsPanel';
import { AuditSummaryPanel } from '@/components/invoiceReview/AuditSummaryPanel';
import { InvoicePdfViewer } from '@/components/invoiceReview/InvoicePdfViewer';
import {
  InvoiceReviewLoadingState,
  InvoiceReviewErrorState,
  InvoiceReviewNotFoundState,
} from '@/components/invoiceReview/InvoiceReviewStates';

/**
 * Invoice Review Screen (WP-016). Entirely read-only: no approve, reject,
 * query, or notes-editing affordance anywhere on this page — those are
 * explicitly out of scope.
 */
export function InvoiceReviewPage() {
  const { invoiceId } = useParams<{ invoiceId: string }>();
  const { invoice, isLoading, error, notFound, retry } = useInvoiceDetail(invoiceId);
  const { previousId, nextId, position, total } = useInvoiceNavigation(invoiceId);

  if (isLoading) {
    return (
      <>
        <PageHeading title="Invoice Review" />
        <InvoiceReviewLoadingState />
      </>
    );
  }

  if (error) {
    return (
      <>
        <PageHeading title="Invoice Review" />
        <InvoiceReviewErrorState message={error} onRetry={retry} />
      </>
    );
  }

  if (notFound || !invoice) {
    return (
      <>
        <PageHeading title="Invoice Review" />
        <InvoiceReviewNotFoundState />
      </>
    );
  }

  return (
    <>
      <PageHeading title={`Invoice ${invoice.invoiceNumber}`} description={invoice.supplierName} />

      <InvoiceReviewNavBar previousId={previousId} nextId={nextId} position={position} total={total} />

      {invoice.isPotentialDuplicate && <DuplicateWarningBanner reason={invoice.duplicateCheckReason} />}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <div className="order-2 lg:order-1">
          <InvoicePdfViewer pdfUrl={invoice.pdfUrl} invoiceNumber={invoice.invoiceNumber} />
        </div>

        <div className="order-1 flex flex-col gap-6 lg:order-2">
          <InvoiceHeaderSummary invoice={invoice} />
          <ExtractedFieldsPanel fields={invoice.extractedFields} />
          <AuditSummaryPanel entries={invoice.auditEntries} />
        </div>
      </div>
    </>
  );
}
