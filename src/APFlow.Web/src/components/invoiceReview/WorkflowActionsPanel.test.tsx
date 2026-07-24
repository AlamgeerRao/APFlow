import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WorkflowActionsPanel } from '@/components/invoiceReview/WorkflowActionsPanel';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import type { InvoiceDetail } from '@/types/invoiceDetail';

function baseInvoice(overrides: Partial<InvoiceDetail> = {}): InvoiceDetail {
  return {
    id: 'inv-gb-002',
    supplierName: 'Yorkshire Skip Supplies',
    invoiceNumber: 'YSS-2201',
    invoiceDate: '2026-07-02',
    amount: 640,
    currencyCode: 'GBP',
    status: 'CHECKED_READY_TO_APPROVE',
    isPotentialDuplicate: false,
    duplicateCheckReason: null,
    pdfUrl: '/sample-invoices/inv-gb-002.pdf',
    sourceDocumentBlobName: 'gb-skips/2026/07/inv-gb-002.pdf',
    receivedAt: '2026-07-02T07:55:00Z',
    extractedFields: [],
    overallConfidenceScore: 0.9,
    auditEntries: [],
    ...overrides,
  };
}

function renderPanel(roles: string[], invoice: InvoiceDetail, onStatusChanged = vi.fn()) {
  const authValue: AuthContextValue = {
    user: { tenantId: 'gb-skips', tenantName: 'GB Skips', displayName: 'Test User', roles },
    isAuthenticated: true,
    signIn: () => {},
    signOut: () => {},
  };

  return render(
    <AuthContext.Provider value={authValue}>
      <WorkflowActionsPanel invoice={invoice} onStatusChanged={onStatusChanged} />
    </AuthContext.Provider>,
  );
}

describe('WorkflowActionsPanel', () => {
  it('a FINANCE_MANAGER user sees the Approve action (acceptance criteria)', async () => {
    renderPanel(['FINANCE_MANAGER'], baseInvoice());

    await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument());
  });

  it('an AP_REVIEWER user does not see the Approve action, but does see Escalate to Febina (acceptance criteria)', async () => {
    renderPanel(['AP_REVIEWER'], baseInvoice());

    await waitFor(() => expect(screen.getByRole('button', { name: 'Escalate to Febina' })).toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
  });

  it('displays the current status via its tenant-driven display label (task 4)', async () => {
    renderPanel(['AP_REVIEWER'], baseInvoice({ status: 'AWAITING_REVIEW' }));

    await waitFor(() => expect(screen.getByText('Awaiting Review')).toBeInTheDocument());
  });

  it('requires confirmation before executing an action, and does not call onStatusChanged until confirmed (task 5)', async () => {
    const onStatusChanged = vi.fn();
    const user = userEvent.setup();
    renderPanel(['FINANCE_MANAGER'], baseInvoice(), onStatusChanged);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'Approve' }));

    expect(screen.getByRole('alertdialog')).toBeInTheDocument();
    expect(onStatusChanged).not.toHaveBeenCalled();
  });

  it('cancelling the confirmation dismisses it without executing anything', async () => {
    const onStatusChanged = vi.fn();
    const user = userEvent.setup();
    renderPanel(['FINANCE_MANAGER'], baseInvoice(), onStatusChanged);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'Approve' }));
    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(onStatusChanged).not.toHaveBeenCalled();
  });

  it('executes the action after confirmation, shows a success notification, and calls onStatusChanged (tasks 5, 6, 7)', async () => {
    const onStatusChanged = vi.fn();
    const user = userEvent.setup();
    // Distinct invoice id from other test files' mutations, and from this
    // file's own other tests — CHECKED_READY_TO_APPROVE is only actually
    // executed against in this one test.
    renderPanel(['FINANCE_MANAGER'], baseInvoice({ id: 'inv-gb-002' }), onStatusChanged);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'Approve' }));
    await user.click(screen.getByRole('button', { name: 'Confirm' }));

    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent(/successfully/i));
    expect(onStatusChanged).toHaveBeenCalledTimes(1);
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });
});
