import { useState } from 'react';
import type { InvoiceDetail } from '@/types/invoiceDetail';
import type { WorkflowAction } from '@/types/workflowAction';
import { useWorkflowActions } from '@/api/useWorkflowActions';
import { InvoiceStatusBadge } from '@/components/invoiceQueue/InvoiceStatusBadge';
import { WorkflowActionButtons } from '@/components/invoiceReview/WorkflowActionButtons';
import { ConfirmActionDialog } from '@/components/invoiceReview/ConfirmActionDialog';

interface WorkflowActionsPanelProps {
  invoice: InvoiceDetail;
  /**
   * Called after a status change is successfully executed, with the full
   * updated invoice (WP-020: WP-054's PATCH endpoint already returns the
   * updated `InvoiceDetail`, so the caller applies it directly via
   * `useInvoiceDetail`'s `applyUpdatedInvoice` — no second network round
   * trip, unlike WP-018's original `retry()`-based design).
   */
  onStatusChanged: (updated: InvoiceDetail) => void;
  /**
   * Called after a note is successfully created as part of a transition
   * (WP-084) - the caller uses this to refresh the Notes panel, which
   * otherwise has no way to know a note was created server-side, atomically
   * with the status change, rather than through its own `AddNoteForm`.
   */
  onNoteCreated?: () => void;
}

/**
 * Invoice Workflow Actions panel (WP-018): displays the invoice's current
 * status via the tenant's own `StatusReference` display name (task 4,
 * reusing the existing `InvoiceStatusBadge`/`useWorkflowTemplate` — not a
 * new, duplicate status display), and renders exactly the action buttons
 * the acting tenant's workflow configuration and the acting user's role
 * permit for that status (tasks 1–3).
 */
export function WorkflowActionsPanel({ invoice, onStatusChanged, onNoteCreated }: WorkflowActionsPanelProps) {
  const { actions, isLoading, error, isExecuting, executeError, executeAction } = useWorkflowActions(
    invoice.id,
    invoice.status,
  );
  const [pendingAction, setPendingAction] = useState<WorkflowAction | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  async function handleConfirm(notes: string) {
    if (!pendingAction) return;

    setSuccessMessage(null);
    const updated = await executeAction(pendingAction, notes);
    if (updated) {
      setSuccessMessage(`Invoice marked as "${pendingAction.targetStatusLabel}" successfully.`);
      setPendingAction(null);
      // The PATCH response never carries pdfUrl (see
      // invoiceDetailMapping.ts) — the document hasn't changed, so reuse
      // the already-resolved blob URL rather than re-fetching the PDF.
      onStatusChanged({ ...updated, pdfUrl: invoice.pdfUrl });
      // WP-084: the note was just created atomically with the status change
      // on the server - nothing else in this component's own state knows
      // that, so tell the caller to refresh the (separately-owned) Notes
      // panel rather than leaving it stale until the next full page load.
      onNoteCreated?.();
    }
    // On failure, leave the confirmation open with executeError displayed
    // (from the hook) so the user can see why and decide whether to retry.
  }

  function handleSelect(action: WorkflowAction) {
    setSuccessMessage(null);
    setPendingAction(action);
  }

  function handleCancel() {
    setPendingAction(null);
  }

  return (
    <section aria-labelledby="workflow-actions-heading" className="rounded-md border border-slate-200 bg-white p-4">
      <div className="mb-3 flex items-center justify-between">
        <h2 id="workflow-actions-heading" className="text-sm font-semibold text-ink-900">
          Workflow Actions
        </h2>
        <InvoiceStatusBadge statusCode={invoice.status} />
      </div>

      {isLoading && <p className="text-sm text-slate-600">Loading available actions...</p>}

      {!isLoading && error && <p role="alert" className="text-sm text-red-600">{error}</p>}

      {!isLoading && !error && (
        <WorkflowActionButtons actions={actions} onSelect={handleSelect} disabled={isExecuting} />
      )}

      {pendingAction && (
        <ConfirmActionDialog
          // WP-084: keyed on the target status so switching which action is
          // pending (possible without cancelling first - WorkflowActionButtons
          // only disables while isExecuting, not while merely pending) forces
          // a fresh mount, resetting the dialog's own note text rather than
          // carrying a note typed for one transition over to a different one.
          key={pendingAction.targetStatusCode}
          action={pendingAction}
          onConfirm={handleConfirm}
          onCancel={handleCancel}
          isSubmitting={isExecuting}
        />
      )}

      {executeError && (
        <p role="alert" className="mt-3 text-sm text-red-600">
          {executeError}
        </p>
      )}

      {successMessage && (
        <p role="status" className="mt-3 text-sm text-green-700">
          {successMessage}
        </p>
      )}
    </section>
  );
}
