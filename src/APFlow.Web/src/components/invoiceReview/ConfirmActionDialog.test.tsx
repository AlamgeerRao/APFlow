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

  it('calls onConfirm when Confirm is clicked', async () => {
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

    await user.click(screen.getByRole('button', { name: 'Confirm' }));

    expect(onConfirm).toHaveBeenCalled();
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

  it('disables both buttons while submitting', () => {
    render(
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
