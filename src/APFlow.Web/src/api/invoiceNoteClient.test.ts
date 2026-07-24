import { describe, expect, it } from 'vitest';
import { FixtureInvoiceNoteClient } from '@/api/invoiceNoteClient';
import { INVOICE_NOTE_CONTENT_MAX_LENGTH } from '@/types/invoiceNote';

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
