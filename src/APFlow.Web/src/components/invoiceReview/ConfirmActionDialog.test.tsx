import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ConfirmActionDialog } from '@/components/invoiceReview/ConfirmActionDialog';

describe('ConfirmActionDialog', () => {
  it('shows a confirmation message naming the action (task 5)', () => {
    render(
      <ConfirmActionDialog
        action={{ targetStatusCode: 'APPROVED', targetStatusLabel: 'Approve' }}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
        isSubmitting={false}
      />,
    );

    expect(screen.getByRole('alertdialog')).toHaveTextContent(/Approve/);
  });

  it('shows clear messaging that a note is required (WP-084)', () => {
    render(
      <ConfirmActionDialog
        action={{ targetStatusCode: 'APPROVED', targetStatusLabel: 'Approve' }}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
        isSubmitting={false}
      />,
    );

    expect(screen.getByText(/A note explaining this action is required/i)).toBeInTheDocument();
  });

  it('Confirm is disabled with no note entered (WP-084)', () => {
    render(
      <ConfirmActionDialog
        action={{ targetStatusCode: 'APPROVED', targetStatusLabel: 'Approve' }}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
        isSubmitting={false}
      />,
    );

    expect(screen.getByRole('button', { name: 'Confirm' })).toBeDisabled();
  });

  it('Confirm stays disabled for whitespace-only text (WP-084)', async () => {
    const user = userEvent.setup();
    render(
      <ConfirmActionDialog
        action={{ targetStatusCode: 'APPROVED', targetStatusLabel: 'Approve' }}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
        isSubmitting={false}
      />,
    );

    await user.type(screen.getByLabelText('Note'), '   ');

    expect(screen.getByRole('button', { name: 'Confirm' })).toBeDisabled();
  });

  it('Confirm becomes enabled once real text is entered, and calls onConfirm with the trimmed note (WP-084)', async () => {
    const onConfirm = vi.fn();
    const user = userEvent.setup();
    render(
      <ConfirmActionDialog
        action={{ targetStatusCode: 'APPROVED', targetStatusLabel: 'Approve' }}
        onConfirm={onConfirm}
        onCancel={vi.fn()}
        isSubmitting={false}
      />,
    );

    const confirmButton = screen.getByRole('button', { name: 'Confirm' });
    expect(confirmButton).toBeDisabled();

    await user.type(screen.getByLabelText('Note'), '  Looks correct, approving.  ');
    expect(confirmButton).toBeEnabled();

    await user.click(confirmButton);

    expect(onConfirm).toHaveBeenCalledWith('Looks correct, approving.');
  });

  it('calls onCancel when Cancel is clicked', async () => {
    const onCancel = vi.fn();
    const user = userEvent.setup();
    render(
      <ConfirmActionDialog
        action={{ targetStatusCode: 'APPROVED', targetStatusLabel: 'Approve' }}
        onConfirm={vi.fn()}
        onCancel={onCancel}
        isSubmitting={false}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(onCancel).toHaveBeenCalled();
  });

  it('disables both buttons while submitting, even with a note entered', async () => {
    const user = userEvent.setup();
    const { rerender } = render(
      <ConfirmActionDialog
        action={{ targetStatusCode: 'APPROVED', targetStatusLabel: 'Approve' }}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
        isSubmitting={false}
      />,
    );

    await user.type(screen.getByLabelText('Note'), 'A note.');

    rerender(
      <ConfirmActionDialog
        action={{ targetStatusCode: 'APPROVED', targetStatusLabel: 'Approve' }}
        onConfirm={vi.fn()}
        onCancel={vi.fn()}
        isSubmitting={true}
      />,
    );

    expect(screen.getByRole('button', { name: /confirming/i })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Cancel' })).toBeDisabled();
  });
});
