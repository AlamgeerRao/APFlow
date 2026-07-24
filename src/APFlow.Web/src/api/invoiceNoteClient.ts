import type { InvoiceNote } from '@/types/invoiceNote';
import { INVOICE_NOTE_CONTENT_MAX_LENGTH } from '@/types/invoiceNote';
import { invoiceNoteFixturesById } from '@/api/fixtures/invoiceNotes.fixture';

/**
 * Client-side contract for reading and adding notes on a single invoice
 * (WP-017). No real HTTP endpoint exists yet — see
 * docs/WP-017-Invoice-Notes-Decisions.md for the proposed contract. All
 * consumers (`useInvoiceNotes`) depend only on this interface — swapping in
 * a real HTTP client is a one-line change (`invoiceNoteClient` below).
 *
 * Deliberately two separate methods rather than one "get invoice detail
 * including notes" call: notes have their own, independent write path (add)
 * that the rest of the invoice detail does not, so a dedicated
 * list/add pair on their own resource is the shape most likely to match
 * whatever the backend ships (`GET`/`POST api/invoices/{id}/notes}`),
 * consistent with WP-015/WP-016's precedent of shaping the client contract
 * around the resource being added, not the page that happens to display it.
 */
export interface InvoiceNoteClient {
  /** Returns every note recorded against the given invoice, in no particular order (sorting is the caller's responsibility — see `useInvoiceNotes`). */
  getNotes(tenantId: string, invoiceId: string): Promise<InvoiceNote[]>;

  /**
   * Adds a new note to the given invoice and returns the created note.
   * Mirrors the backend's own `InvoiceService.AddNoteAsync` validation
   * (non-empty, non-whitespace-only content up to
   * `INVOICE_NOTE_CONTENT_MAX_LENGTH` characters) so the fixture client's
   * error path matches what a real API would reject with — this is
   * defense-in-depth: `AddNoteForm` already blocks invalid submissions
   * client-side, but per Security Standards §4 ("validate all inputs,
   * including internal method inputs where failure would be costly") this
   * layer must not simply trust that its only caller already checked.
   * Rejects (throws) rather than returning a null/error-shaped result — the
   * only other client in this codebase with a mutation
   * (`workflowTemplateClient`) has no mutating method to compare against, so
   * this follows the more common `useState`/`try-catch` handling already
   * used by `useInvoiceDetail`/`useInvoiceQueue` for their own async calls.
   */
  addNote(tenantId: string, invoiceId: string, content: string, authorName: string): Promise<InvoiceNote>;
}

/**
 * Temporary fixture-backed implementation, holding notes in memory (module
 * scope, so additions persist across re-renders but not page reloads — the
 * same lifetime as every other fixture client in this codebase). Seed data
 * for a handful of invoices lives in `invoiceNotes.fixture.ts`; any other
 * invoice id starts with an empty note list rather than an error, since "no
 * notes yet" is an expected, valid state, not a failure.
 */
export class FixtureInvoiceNoteClient implements InvoiceNoteClient {
  private readonly notesById: Record<string, InvoiceNote[]> = structuredClone(invoiceNoteFixturesById);

  async getNotes(_tenantId: string, invoiceId: string): Promise<InvoiceNote[]> {
    // Tenant isolation for real notes would be enforced server-side (the
    // backend's InvoiceNote is TenantEntity-derived — see its own doc
    // comment); this fixture keys purely by invoiceId, so tenantId is
    // accepted (to match the real client's future signature) but unused.
    return this.notesById[invoiceId] ?? [];
  }

  async addNote(_tenantId: string, invoiceId: string, content: string, authorName: string): Promise<InvoiceNote> {
    const trimmed = content.trim();

    if (trimmed.length === 0) {
      throw new Error('Note content must not be empty.');
    }

    if (content.length > INVOICE_NOTE_CONTENT_MAX_LENGTH) {
      throw new Error(`Note content must not exceed ${INVOICE_NOTE_CONTENT_MAX_LENGTH} characters.`);
    }

    const note: InvoiceNote = {
      id: crypto.randomUUID(),
      content,
      authorName,
      createdAtUtc: new Date().toISOString(),
    };

    const existing = this.notesById[invoiceId] ?? [];
    this.notesById[invoiceId] = [...existing, note];

    return note;
  }
}

/**
 * The client instance the app uses. Swap this single line for a real
 * HTTP-backed implementation once the backend contract is confirmed — no
 * other file needs to change.
 */
export const invoiceNoteClient: InvoiceNoteClient = new FixtureInvoiceNoteClient();
