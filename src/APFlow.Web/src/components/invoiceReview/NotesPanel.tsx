import { useEffect } from 'react';
import { useInvoiceNotes } from '@/api/useInvoiceNotes';
import { NotesList } from '@/components/invoiceReview/NotesList';
import { AddNoteForm } from '@/components/invoiceReview/AddNoteForm';

interface NotesPanelProps {
  invoiceId: string;
  /**
   * Bump this (e.g. a counter) whenever a note may have been created outside
   * this panel's own `AddNoteForm` - specifically, WP-084's mandatory
   * per-transition note, created server-side atomically with the status
   * change by `WorkflowActionsPanel`, not through this panel's `addNote`.
   * Without this, the notes list would silently go stale after a workflow
   * action until the next full page load.
   */
  refreshToken?: number;
}

/**
 * Invoice Notes & Collaboration panel (WP-017). A self-contained widget
 * (unlike the other Review Screen panels, which are purely presentational
 * and receive their data as props from `InvoiceReviewPage`'s single
 * `useInvoiceDetail` call): notes have their own independent load-then-add
 * lifecycle distinct from the rest of the invoice detail, so this panel
 * owns `useInvoiceNotes` itself rather than threading note state through
 * the page.
 */
export function NotesPanel({ invoiceId, refreshToken }: NotesPanelProps) {
  const { notes, isLoading, error, isSubmitting, submitError, addNote, retry } = useInvoiceNotes(invoiceId);

  useEffect(() => {
    if (refreshToken !== undefined) {
      retry();
    }
    // Deliberately reacts to refreshToken changing only - retry is stable
    // (useCallback in useInvoiceNotes) but including it here too is
    // harmless and keeps the lint rule honest about the real dependency.
  }, [refreshToken, retry]);

  return (
    <section aria-labelledby="notes-heading" className="rounded-md border border-slate-200 bg-white p-4">
      <h2 id="notes-heading" className="mb-3 text-sm font-semibold text-ink-900">
        Notes
      </h2>

      {isLoading && <p className="text-sm text-slate-600">Loading notes...</p>}

      {!isLoading && error && (
        <div role="alert" className="text-sm text-red-600">
          <p>{error}</p>
          <button type="button" onClick={retry} className="mt-1 font-medium underline hover:no-underline">
            Try again
          </button>
        </div>
      )}

      {!isLoading && !error && <NotesList notes={notes} />}

      <AddNoteForm onSubmit={addNote} isSubmitting={isSubmitting} submitError={submitError} />
    </section>
  );
}
