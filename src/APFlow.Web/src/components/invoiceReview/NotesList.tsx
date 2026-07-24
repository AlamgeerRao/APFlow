import type { InvoiceNote } from '@/types/invoiceNote';
import { formatDateTime } from '@/utils/format';

interface NotesListProps {
  /** Expected to already be sorted chronologically by the caller (see `useInvoiceNotes`) — this component only renders, it does not sort. */
  notes: InvoiceNote[];
}

/**
 * Read-only list of an invoice's notes, oldest first (WP-017 tasks 1, 3, 4,
 * 5). No edit or delete affordance exists anywhere here, per WP-017's
 * explicit "Do not allow editing or deleting notes."
 */
export function NotesList({ notes }: NotesListProps) {
  if (notes.length === 0) {
    return <p className="text-sm text-slate-600">No notes yet. Add the first one below.</p>;
  }

  return (
    <ol className="space-y-3">
      {notes.map((note) => (
        <li key={note.id} className="border-l-2 border-slate-200 pl-3">
          {/* whitespace-pre-wrap preserves line breaks for multiline notes (task 5) without needing dangerouslySetInnerHTML. */}
          <p className="whitespace-pre-wrap text-sm text-ink-900">{note.content}</p>
          <p className="mt-1 text-xs text-slate-400">
            {note.authorName} · {formatDateTime(note.createdAtUtc)}
          </p>
        </li>
      ))}
    </ol>
  );
}
