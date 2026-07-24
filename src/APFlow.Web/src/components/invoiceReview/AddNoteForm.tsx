import { useId, useState } from 'react';
import type { FormEvent } from 'react';
import { INVOICE_NOTE_CONTENT_MAX_LENGTH } from '@/types/invoiceNote';

interface AddNoteFormProps {
  /** Returns true if the note was saved successfully. The form only clears itself on true. */
  onSubmit: (content: string) => Promise<boolean>;
  isSubmitting: boolean;
  /** Submission-time error from the parent (e.g. the client rejected the request) — distinct from this form's own client-side validation errors. */
  submitError: string | null;
}

/**
 * Form for adding a new note (WP-017 tasks 2, 5, 7). A `<textarea>` is used
 * rather than a single-line `<input>` specifically to support multiline
 * notes (task 5). Validation mirrors the backend's own rules
 * (`InvoiceService.AddNoteAsync`): content must not be empty or
 * whitespace-only, and must not exceed
 * `INVOICE_NOTE_CONTENT_MAX_LENGTH` characters.
 */
export function AddNoteForm({ onSubmit, isSubmitting, submitError }: AddNoteFormProps) {
  const [content, setContent] = useState('');
  const [validationError, setValidationError] = useState<string | null>(null);
  const textareaId = useId();

  const remaining = INVOICE_NOTE_CONTENT_MAX_LENGTH - content.length;

  function validate(value: string): string | null {
    if (value.trim().length === 0) {
      return 'Enter a note before saving.';
    }
    if (value.length > INVOICE_NOTE_CONTENT_MAX_LENGTH) {
      return `Note must not exceed ${INVOICE_NOTE_CONTENT_MAX_LENGTH} characters.`;
    }
    return null;
  }

  async function handleSubmit(event: FormEvent) {
    event.preventDefault();

    const error = validate(content);
    setValidationError(error);
    if (error) return;

    const succeeded = await onSubmit(content);
    if (succeeded) {
      setContent('');
    }
    // On failure, deliberately leave the typed content in place (via
    // `submitError`, passed back in as a prop) so the user doesn't lose
    // what they wrote and can just retry.
  }

  const displayedError = validationError ?? submitError;

  return (
    <form onSubmit={handleSubmit} className="mt-4 border-t border-slate-100 pt-4">
      <label htmlFor={textareaId} className="mb-1 block text-sm font-medium text-ink-900">
        Add a note
      </label>
      <textarea
        id={textareaId}
        value={content}
        onChange={(event) => {
          setContent(event.target.value);
          if (validationError) setValidationError(null);
        }}
        rows={3}
        disabled={isSubmitting}
        aria-invalid={displayedError ? true : undefined}
        aria-describedby={displayedError ? `${textareaId}-error` : undefined}
        className="block w-full rounded-md border border-slate-200 p-2 text-sm text-ink-900 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600 disabled:bg-slate-50 disabled:text-slate-400"
        placeholder="Record a comment for anyone reviewing this invoice..."
      />

      <div className="mt-1 flex items-center justify-between">
        <div>
          {displayedError && (
            <p id={`${textareaId}-error`} role="alert" className="text-xs text-red-600">
              {displayedError}
            </p>
          )}
        </div>
        <p className="text-xs text-slate-400">{remaining} characters remaining</p>
      </div>

      <button
        type="submit"
        disabled={isSubmitting}
        className="mt-2 rounded-md bg-accent-600 px-3 py-1.5 text-sm font-medium text-white hover:bg-accent-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600 disabled:cursor-not-allowed disabled:bg-slate-300"
      >
        {isSubmitting ? 'Saving...' : 'Save note'}
      </button>
    </form>
  );
}
