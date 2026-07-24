import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '@/auth/useAuth';
import { invoiceNoteClient } from '@/api/invoiceNoteClient';
import type { InvoiceNote } from '@/types/invoiceNote';

interface InvoiceNotesState {
  notes: InvoiceNote[];
  isLoading: boolean;
  error: string | null;
  /** True while a new note is being submitted — disables the form's submit control. */
  isSubmitting: boolean;
  /** Validation/submission error from the most recent add attempt, if any. */
  submitError: string | null;
  /** Adds a note, then reloads the full list from the client (task 6: "Refresh notes after save"). Returns true on success, false on failure (see `submitError` for the message) — the caller (`AddNoteForm`) uses this to decide whether it's safe to clear the input. */
  addNote: (content: string) => Promise<boolean>;
  retry: () => void;
}

/** Sorts notes chronologically (oldest first), matching AuditSummaryPanel's own ordering convention for consistency (WP-016 §6/task 4). */
function sortChronologically(notes: InvoiceNote[]): InvoiceNote[] {
  return [...notes].sort((a, b) => new Date(a.createdAtUtc).getTime() - new Date(b.createdAtUtc).getTime());
}

/**
 * Loads and manages notes for a single invoice's Notes panel (WP-017).
 * Collaboration only — adding a note is the only mutation this hook
 * exposes; there is deliberately no `updateNote`/`deleteNote`, per WP-017's
 * explicit "Do not allow editing or deleting notes."
 */
export function useInvoiceNotes(invoiceId: string | undefined): InvoiceNotesState {
  const { user } = useAuth();
  const [notes, setNotes] = useState<InvoiceNote[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  const loadNotes = useCallback(
    async (tenantId: string, id: string, signal: { cancelled: boolean }) => {
      setIsLoading(true);
      setError(null);
      try {
        const result = await invoiceNoteClient.getNotes(tenantId, id);
        if (signal.cancelled) return;
        setNotes(sortChronologically(result));
        setIsLoading(false);
      } catch {
        if (!signal.cancelled) {
          setError('Unable to load notes for this invoice. Please try again.');
          setIsLoading(false);
        }
      }
    },
    [],
  );

  useEffect(() => {
    if (!user || !invoiceId) {
      return;
    }

    const signal = { cancelled: false };
    // eslint-disable-next-line react-hooks/set-state-in-effect
    void loadNotes(user.tenantId, invoiceId, signal);

    return () => {
      signal.cancelled = true;
    };
  }, [user, invoiceId, reloadToken, loadNotes]);

  const addNote = useCallback(
    async (content: string): Promise<boolean> => {
      if (!user || !invoiceId) return false;

      setIsSubmitting(true);
      setSubmitError(null);
      try {
        await invoiceNoteClient.addNote(user.tenantId, invoiceId, content, user.displayName);
        // Task 6: reload from the client rather than optimistically
        // appending, so the displayed list always reflects what the client
        // (and eventually the real API) actually persisted.
        const signal = { cancelled: false };
        await loadNotes(user.tenantId, invoiceId, signal);
        return true;
      } catch (err) {
        setSubmitError(err instanceof Error ? err.message : 'Unable to save this note. Please try again.');
        return false;
      } finally {
        setIsSubmitting(false);
      }
    },
    [user, invoiceId, loadNotes],
  );

  const noSubject = !user || !invoiceId;

  return {
    notes: noSubject ? [] : notes,
    isLoading: noSubject ? false : isLoading,
    error: noSubject ? null : error,
    isSubmitting,
    submitError,
    addNote,
    retry: () => setReloadToken((t) => t + 1),
  };
}
