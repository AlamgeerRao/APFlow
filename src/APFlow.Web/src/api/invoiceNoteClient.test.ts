import { describe, expect, it, vi, beforeEach } from 'vitest';
import { FixtureInvoiceNoteClient, HttpInvoiceNoteClient } from '@/api/invoiceNoteClient';
import { INVOICE_NOTE_CONTENT_MAX_LENGTH } from '@/types/invoiceNote';
import { httpClient } from '@/api/httpClient';

vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn(), post: vi.fn() } };
});

describe('FixtureInvoiceNoteClient', () => {
  it('returns the seeded notes for an invoice with fixture data', async () => {
    const client = new FixtureInvoiceNoteClient();

    const notes = await client.getNotes('platform-default', 'inv-pd-001');

    expect(notes).toHaveLength(2);
    expect(notes.map((n) => n.id)).toEqual(expect.arrayContaining(['note-pd-001-1', 'note-pd-001-2']));
  });

  it('returns an empty list for an invoice with no notes, rather than an error', async () => {
    const client = new FixtureInvoiceNoteClient();

    const notes = await client.getNotes('platform-default', 'inv-with-no-notes');

    expect(notes).toEqual([]);
  });

  it('adds a note and makes it visible on the next getNotes call (task 6: refresh after save)', async () => {
    const client = new FixtureInvoiceNoteClient();

    const created = await client.addNote('platform-default', 'inv-pd-003', 'A brand new note.', 'Jamie Lee');

    expect(created.content).toBe('A brand new note.');
    expect(created.authorName).toBe('Jamie Lee');
    expect(created.id).toBeTruthy();
    expect(created.createdAtUtc).toBeTruthy();

    const notes = await client.getNotes('platform-default', 'inv-pd-003');
    expect(notes).toHaveLength(1);
    expect(notes[0]).toEqual(created);
  });

  it('preserves multiline content exactly as submitted (task 5)', async () => {
    const client = new FixtureInvoiceNoteClient();
    const multiline = 'Line one.\nLine two.\n\nLine four.';

    const created = await client.addNote('platform-default', 'inv-pd-003', multiline, 'Jamie Lee');

    expect(created.content).toBe(multiline);
  });

  it('rejects empty content', async () => {
    const client = new FixtureInvoiceNoteClient();

    await expect(client.addNote('platform-default', 'inv-pd-003', '', 'Jamie Lee')).rejects.toThrow(
      /must not be empty/i,
    );
  });

  it('rejects whitespace-only content', async () => {
    const client = new FixtureInvoiceNoteClient();

    await expect(client.addNote('platform-default', 'inv-pd-003', '   \n  ', 'Jamie Lee')).rejects.toThrow(
      /must not be empty/i,
    );
  });

  it('rejects content over the max length', async () => {
    const client = new FixtureInvoiceNoteClient();
    const tooLong = 'a'.repeat(INVOICE_NOTE_CONTENT_MAX_LENGTH + 1);

    await expect(client.addNote('platform-default', 'inv-pd-003', tooLong, 'Jamie Lee')).rejects.toThrow(
      /must not exceed/i,
    );
  });

  it('does not leak notes added on one client instance to a fresh instance (in-memory only)', async () => {
    const firstClient = new FixtureInvoiceNoteClient();
    await firstClient.addNote('platform-default', 'inv-pd-003', 'Only on the first instance.', 'Jamie Lee');

    const secondClient = new FixtureInvoiceNoteClient();
    const notes = await secondClient.getNotes('platform-default', 'inv-pd-003');

    expect(notes).toEqual([]);
  });
});

describe('HttpInvoiceNoteClient', () => {
  beforeEach(() => {
    vi.mocked(httpClient.get).mockReset();
    vi.mocked(httpClient.post).mockReset();
  });

  it('maps the real DTO (id/content/authorDisplayName/createdAtUtc) to our InvoiceNote shape on getNotes', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce([
      { id: 'note-1', content: 'Real note content.', authorDisplayName: 'Patrick', createdAtUtc: '2026-07-02T11:20:00Z' },
    ]);
    const client = new HttpInvoiceNoteClient();

    const notes = await client.getNotes('gb-skips', 'inv-gb-001');

    expect(httpClient.get).toHaveBeenCalledWith('/api/invoices/inv-gb-001/notes');
    expect(notes).toEqual([
      { id: 'note-1', content: 'Real note content.', authorName: 'Patrick', createdAtUtc: '2026-07-02T11:20:00Z' },
    ]);
  });

  it('sends only { content } in the POST body — the real client has no author-name parameter to send at all', async () => {
    vi.mocked(httpClient.post).mockResolvedValueOnce({
      id: 'note-2',
      content: 'New note.',
      authorDisplayName: 'Jamie Lee',
      createdAtUtc: '2026-07-10T09:00:00Z',
    });
    const client = new HttpInvoiceNoteClient();

    const created = await client.addNote('platform-default', 'inv-pd-006', 'New note.');

    expect(httpClient.post).toHaveBeenCalledWith('/api/invoices/inv-pd-006/notes', { content: 'New note.' });
    // The server-resolved author comes back regardless of what was passed in.
    expect(created.authorName).toBe('Jamie Lee');
  });
});
