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
   * Called after a status change is successfully executed. The panel's own
   * `useWorkflowActions` only knows about the ACTIONS available for a
   * status — reloading the INVOICE itself (so the new status shows up
   * everywhere it's displayed: this panel, the header summary badge, and
   * eventually the queue) is the invoice detail page's own responsibility,
   * via whatever reload mechanism it already has (`useInvoiceDetail`'s
   * `retry`) — task 6, "Refresh UI after update."
   */
  onStatusChanged: () => void;
}

/**
 * Invoice Workflow Actions panel (WP-018): displays the invoice's current
 * status via the tenant's own `StatusReference` display name (task 4,
 * reusing the existing `InvoiceStatusBadge`/`useWorkflowTemplate` — not a
 * new, duplicate status display), and renders exactly the action buttons
 * the acting tenant's workflow configuration and the acting user's role
 * permit for that status (tasks 1–3).
 */
export function WorkflowActionsPanel({ invoice, onStatusChanged }: WorkflowActionsPanelProps) {
  const { actions, isLoading, error, isExecuting, executeError, executeAction } = useWorkflowActions(
    invoice.id,
    invoice.status,
  );
  const [pendingAction, setPendingAction] = useState<WorkflowAction | null>(null);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  async function handleConfirm() {
    if (!pendingAction) return;

    setSuccessMessage(null);
    const succeeded = await executeAction(pendingAction);
    if (succeeded) {
      setSuccessMessage(`Invoice marked as "${pendingAction.targetStatusLabel}" successfully.`);
      setPendingAction(null);
      onStatusChanged();
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
