import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AddNoteForm } from '@/components/invoiceReview/AddNoteForm';

describe('AddNoteForm', () => {
  it('calls onSubmit with the typed content and clears the field on success (task 2, 6)', async () => {
    const onSubmit = vi.fn().mockResolvedValue(true);
    const user = userEvent.setup();
    render(<AddNoteForm onSubmit={onSubmit} isSubmitting={false} submitError={null} />);

    await user.type(screen.getByLabelText(/add a note/i), 'A new note.');
    await user.click(screen.getByRole('button', { name: /save note/i }));

    expect(onSubmit).toHaveBeenCalledWith('A new note.');
    expect(screen.getByLabelText(/add a note/i)).toHaveValue('');
  });

  it('supports multiline input (task 5)', async () => {
    const onSubmit = vi.fn().mockResolvedValue(true);
    const user = userEvent.setup();
    render(<AddNoteForm onSubmit={onSubmit} isSubmitting={false} submitError={null} />);

    await user.type(screen.getByLabelText(/add a note/i), 'Line one.{Enter}Line two.');
    await user.click(screen.getByRole('button', { name: /save note/i }));

    expect(onSubmit).toHaveBeenCalledWith('Line one.\nLine two.');
  });

  it('rejects an empty note without calling onSubmit (task 7)', async () => {
    const onSubmit = vi.fn();
    const user = userEvent.setup();
    render(<AddNoteForm onSubmit={onSubmit} isSubmitting={false} submitError={null} />);

    await user.click(screen.getByRole('button', { name: /save note/i }));

    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByRole('alert')).toHaveTextContent(/enter a note/i);
  });

  it('rejects a whitespace-only note without calling onSubmit (task 7)', async () => {
    const onSubmit = vi.fn();
    const user = userEvent.setup();
    render(<AddNoteForm onSubmit={onSubmit} isSubmitting={false} submitError={null} />);

    await user.type(screen.getByLabelText(/add a note/i), '   ');
    await user.click(screen.getByRole('button', { name: /save note/i }));

    expect(onSubmit).not.toHaveBeenCalled();
    expect(screen.getByRole('alert')).toHaveTextContent(/enter a note/i);
  });

  it('keeps the typed content when the save fails, and surfaces the submission error', async () => {
    const onSubmit = vi.fn().mockResolvedValue(false);
    const user = userEvent.setup();
    render(<AddNoteForm onSubmit={onSubmit} isSubmitting={false} submitError="Unable to save this note. Please try again." />);

    await user.type(screen.getByLabelText(/add a note/i), 'Content that fails to save.');
    await user.click(screen.getByRole('button', { name: /save note/i }));

    expect(screen.getByLabelText(/add a note/i)).toHaveValue('Content that fails to save.');
    expect(screen.getByRole('alert')).toHaveTextContent(/unable to save this note/i);
  });

  it('disables the textarea and submit button while submitting', () => {
    render(<AddNoteForm onSubmit={vi.fn()} isSubmitting={true} submitError={null} />);

    expect(screen.getByLabelText(/add a note/i)).toBeDisabled();
    expect(screen.getByRole('button', { name: /saving/i })).toBeDisabled();
  });
});
