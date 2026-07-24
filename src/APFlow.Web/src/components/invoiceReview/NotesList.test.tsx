import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { NotesList } from '@/components/invoiceReview/NotesList';

describe('NotesList', () => {
  it('renders each note with its content, author, and formatted date/time (task 3)', () => {
    render(
      <NotesList
        notes={[
          { id: 'note-1', content: 'Called the supplier to confirm.', authorName: 'Priya Shah', createdAtUtc: '2026-07-01T14:05:00Z' },
        ]}
      />,
    );

    expect(screen.getByText('Called the supplier to confirm.')).toBeInTheDocument();
    expect(screen.getByText(/Priya Shah/)).toBeInTheDocument();
    expect(screen.getByText(/01 Jul 2026, 14:05/)).toBeInTheDocument();
  });

  it('renders notes in the order given, without re-sorting (sorting is the caller\'s responsibility)', () => {
    render(
      <NotesList
        notes={[
          { id: 'note-a', content: 'First.', authorName: 'A', createdAtUtc: '2026-07-01T09:00:00Z' },
          { id: 'note-b', content: 'Second.', authorName: 'B', createdAtUtc: '2026-07-01T10:00:00Z' },
        ]}
      />,
    );

    const items = screen.getAllByRole('listitem');
    expect(items[0]).toHaveTextContent('First.');
    expect(items[1]).toHaveTextContent('Second.');
  });

  it('preserves line breaks in multiline notes (task 5)', () => {
    render(
      <NotesList
        notes={[
          { id: 'note-1', content: 'Line one.\nLine two.', authorName: 'A', createdAtUtc: '2026-07-01T09:00:00Z' },
        ]}
      />,
    );

    const content = screen.getByText((_, element) => element?.textContent === 'Line one.\nLine two.');
    expect(content).toHaveClass('whitespace-pre-wrap');
  });

  it('shows an empty-state message when there are no notes', () => {
    render(<NotesList notes={[]} />);

    expect(screen.getByText(/No notes yet/i)).toBeInTheDocument();
  });

  it('never renders an edit or delete affordance', () => {
    render(
      <NotesList
        notes={[{ id: 'note-1', content: 'Some note.', authorName: 'A', createdAtUtc: '2026-07-01T09:00:00Z' }]}
      />,
    );

    expect(screen.queryByRole('button', { name: /edit/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /delete/i })).not.toBeInTheDocument();
  });
});
