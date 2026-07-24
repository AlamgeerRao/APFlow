import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WorkflowActionButtons } from '@/components/invoiceReview/WorkflowActionButtons';

describe('WorkflowActionButtons', () => {
  it('renders a button for each available action, using its own label (task 1, 2)', () => {
    render(
      <WorkflowActionButtons
        actions={[
          { targetStatusCode: 'CHECKED_READY_TO_APPROVE', targetStatusLabel: 'Mark Checked & Ready to Approve' },
          { targetStatusCode: 'NEEDS_REVIEW_FEBINA', targetStatusLabel: 'Escalate to Febina' },
        ]}
        onSelect={vi.fn()}
        disabled={false}
      />,
    );

    expect(screen.getByRole('button', { name: 'Mark Checked & Ready to Approve' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Escalate to Febina' })).toBeInTheDocument();
  });

  it('shows an empty-state message when no actions are available', () => {
    render(<WorkflowActionButtons actions={[]} onSelect={vi.fn()} disabled={false} />);

    expect(screen.getByText(/No actions are available/i)).toBeInTheDocument();
  });

  it('calls onSelect with the clicked action', async () => {
    const onSelect = vi.fn();
    const user = userEvent.setup();
    render(
      <WorkflowActionButtons
        actions={[{ targetStatusCode: 'APPROVED', targetStatusLabel: 'Approve' }]}
        onSelect={onSelect}
        disabled={false}
      />,
    );

    await user.click(screen.getByRole('button', { name: 'Approve' }));

    expect(onSelect).toHaveBeenCalledWith({ targetStatusCode: 'APPROVED', targetStatusLabel: 'Approve' });
  });

  it('disables every button when disabled is true', () => {
    render(
      <WorkflowActionButtons actions={[{ targetStatusCode: 'APPROVED', targetStatusLabel: 'Approve' }]} onSelect={vi.fn()} disabled={true} />,
    );

    expect(screen.getByRole('button', { name: 'Approve' })).toBeDisabled();
  });
});
