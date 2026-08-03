import { useState } from 'react';
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
import { NotesPanel } from '@/components/invoiceReview/NotesPanel';
import { WorkflowActionsPanel } from '@/components/invoiceReview/WorkflowActionsPanel';
import type { InvoiceDetail } from '@/types/invoiceDetail';
import {
  InvoiceReviewLoadingState,
  InvoiceReviewErrorState,
  InvoiceReviewNotFoundState,
} from '@/components/invoiceReview/InvoiceReviewStates';

/**
 * Invoice Review Screen (WP-016, extended by WP-017's Notes panel and
 * WP-018's Workflow Actions panel). Payment, remittance, and supplier email
 * remain explicitly out of scope (WP-018's own Out of Scope list) — no
 * affordance for any of those exists anywhere on this page.
 *
 * WP-084: Notes now renders directly under Workflow Actions (not after
 * Extracted Fields/Audit Summary, a separately-scrolled section) - every
 * transition requires a note, so the two are placed together deliberately.
 * `notesRefreshToken` starts `undefined` (not `0`) so `NotesPanel` doesn't
 * fire a redundant extra fetch on first mount - see its own doc comment;
 * it only becomes a real number once a workflow action has actually
 * created a note server-side, which is the one case this page needs to
 * tell the independently-owned Notes panel to reload itself.
 */
export function InvoiceReviewPage() {
  const { invoiceId } = useParams<{ invoiceId: string }>();
  const { invoice, isLoading, error, notFound, retry, applyUpdatedInvoice } = useInvoiceDetail(invoiceId);
  const { previousId, nextId, position, total } = useInvoiceNavigation(invoiceId);
  const [notesRefreshToken, setNotesRefreshToken] = useState<number | undefined>(undefined);

  function handleStatusChanged(updated: InvoiceDetail) {
    applyUpdatedInvoice(updated);
  }

  function handleNoteCreated() {
    setNotesRefreshToken((token) => (token ?? 0) + 1);
  }

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
      <PageHeading title={`Invoice ${invoice.invoiceNumber ?? '—'}`} description={invoice.supplierName ?? undefined} />

      <InvoiceReviewNavBar previousId={previousId} nextId={nextId} position={position} total={total} />

      {invoice.isPotentialDuplicate && (
        <DuplicateWarningBanner
          reason={invoice.duplicateCheckReason}
          duplicateMatchInvoiceId={invoice.duplicateMatchInvoiceId}
        />
      )}

      <div className="grid grid-cols-1 gap-6 lg:grid-cols-2">
        <div className="order-2 lg:order-1">
          <InvoicePdfViewer pdfUrl={invoice.pdfUrl} invoiceNumber={invoice.invoiceNumber} />
        </div>

        <div className="order-1 flex flex-col gap-6 lg:order-2">
          <WorkflowActionsPanel invoice={invoice} onStatusChanged={handleStatusChanged} onNoteCreated={handleNoteCreated} />
          <NotesPanel invoiceId={invoice.id} refreshToken={notesRefreshToken} />
          <InvoiceHeaderSummary invoice={invoice} />
          <ExtractedFieldsPanel fields={invoice.extractedFields} />
          <AuditSummaryPanel entries={invoice.auditEntries} />
        </div>
      </div>
    </>
  );
}
