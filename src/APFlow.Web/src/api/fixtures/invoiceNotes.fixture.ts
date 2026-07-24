import type { InvoiceNote } from '@/types/invoiceNote';

/**
 * Seed notes per invoice id, deliberately stored out of chronological order
 * for some invoices so the client's sort-before-display logic is genuinely
 * exercised rather than happening to already match insertion order (see
 * `FixtureInvoiceNoteClient` and `useInvoiceNotes`).
 */
export const invoiceNoteFixturesById: Record<string, InvoiceNote[]> = {
  'inv-pd-001': [
    {
      id: 'note-pd-001-2',
      content: 'Chased the supplier by phone — they confirmed the PO number and will resend a corrected copy.',
      authorName: 'Priya Shah',
      createdAtUtc: '2026-07-01T14:05:00Z',
    },
    {
      id: 'note-pd-001-1',
      content: 'VAT number on this one looks slightly off compared to their last invoice — worth double-checking before approval.',
      authorName: 'Tom Whitfield',
      createdAtUtc: '2026-07-01T09:30:00Z',
    },
  ],
  'inv-pd-002': [
    {
      id: 'note-pd-002-1',
      content:
        'Flagged as a possible duplicate by the system.\n\nChecked manually against invoice CS-1998 from last month — different job reference and delivery address, so this looks genuine. Recommending we proceed once the low-confidence fields are verified.',
      authorName: 'Priya Shah',
      createdAtUtc: '2026-07-03T10:15:00Z',
    },
  ],
  'inv-gb-001': [
    {
      id: 'note-gb-001-1',
      content: 'Approved for payment as part of the usual monthly Yorkshire Skip Supplies run.',
      authorName: 'Patrick',
      createdAtUtc: '2026-07-02T11:20:00Z',
    },
  ],
};
