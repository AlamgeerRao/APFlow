import { useState } from 'react';
import type { WorkflowAction } from '@/types/workflowAction';

interface ConfirmActionDialogProps {
  action: WorkflowAction;
  onConfirm: (notes: string) => void;
  onCancel: () => void;
  isSubmitting: boolean;
}

/**
 * Inline confirmation step shown before a status change is executed
 * (WP-018 task 5: "Require confirmation before status changes"). A simple
 * inline banner rather than a modal dialog/overlay — this avoids adding a
 * new modal/focus-trap dependency for a single yes/no decision, per
 * Simplicity First (`02_Project_Standards.md` §1); `role="alertdialog"`
 * still gives it the right assistive-technology semantics for an
 * in-context confirmation prompt.
 *
 * WP-084: every transition now requires a non-empty note before it can be
 * confirmed — enforced client-side (Confirm stays disabled until real text
 * is entered, not just rejected after the fact) as well as server-side
 * (`InvoiceService.UpdateAsync` rejects a missing/empty note outright).
 * Local, uncontrolled-from-outside text state: the note is specific to
 * *this* confirmation attempt and is discarded on cancel, not lifted to
 * `WorkflowActionsPanel` — nothing else needs it mid-typing.
 */
export function ConfirmActionDialog({ action, onConfirm, onCancel, isSubmitting }: ConfirmActionDialogProps) {
  const [notes, setNotes] = useState('');
  const trimmedNotes = notes.trim();
  const canConfirm = trimmedNotes.length > 0 && !isSubmitting;

  return (
    <div
      role="alertdialog"
      aria-labelledby="confirm-action-heading"
      className="mt-3 rounded-md border border-accent-600 bg-slate-50 p-3"
    >
      <p id="confirm-action-heading" className="text-sm text-ink-900">
        {`Are you sure you want to "${action.targetStatusLabel}"?`}
      </p>

      <div className="mt-2">
        <label htmlFor="confirm-action-notes" className="block text-sm font-medium text-ink-900">
          Note
        </label>
        <p id="confirm-action-notes-hint" className="mb-1 text-xs text-slate-600">
          A note explaining this action is required.
        </p>
        <textarea
          id="confirm-action-notes"
          value={notes}
          onChange={(event) => setNotes(event.target.value)}
          disabled={isSubmitting}
          rows={2}
          aria-describedby="confirm-action-notes-hint"
          aria-required="true"
          className="w-full rounded-md border border-slate-300 p-2 text-sm text-ink-900 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600 disabled:cursor-not-allowed disabled:bg-slate-100"
        />
      </div>

      <div className="mt-2 flex gap-2">
        <button
          type="button"
          onClick={() => onConfirm(trimmedNotes)}
          disabled={!canConfirm}
          className="rounded-md bg-accent-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600 disabled:cursor-not-allowed disabled:bg-slate-300"
        >
          {isSubmitting ? 'Confirming...' : 'Confirm'}
        </button>
        <button
          type="button"
          onClick={onCancel}
          disabled={isSubmitting}
          className="rounded-md border border-slate-200 bg-white px-3 py-1.5 text-sm font-medium text-ink-900 hover:bg-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600 disabled:cursor-not-allowed disabled:text-slate-400"
        >
          Cancel
        </button>
      </div>
    </div>
  );
}
