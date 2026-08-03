import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { WorkflowActionsPanel } from '@/components/invoiceReview/WorkflowActionsPanel';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import type { InvoiceDetail } from '@/types/invoiceDetail';
import { httpClient } from '@/api/httpClient';

// WP-020: available actions/execution now come from the real API; mock
// httpClient directly rather than depending on the (now largely retired)
// fixture transition graph.
vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn(), patch: vi.fn() } };
});

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
    duplicateMatchInvoiceId: null,
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

const financeManagerActions = [
  { targetStatusCode: 'APPROVED', targetStatusLabel: 'Approve' },
  { targetStatusCode: 'NEEDS_QUERY', targetStatusLabel: 'Send Query' },
  { targetStatusCode: 'NEEDS_REVIEW_FEBINA', targetStatusLabel: 'Escalate to Febina' },
];
const apReviewerActions = [{ targetStatusCode: 'NEEDS_REVIEW_FEBINA', targetStatusLabel: 'Escalate to Febina' }];

// WP-075: InvoiceStatusBadge (rendered by WorkflowActionsPanel itself, for the
// current-status label) now also calls real GET /api/workflow-template, in
// parallel with useWorkflowActions' own GET .../available-actions call. Both
// go through this same mocked httpClient.get, so the mock must route by path
// rather than using a single mockResolvedValueOnce per test (which only ever
// satisfies whichever of the two calls happens to run first).
const workflowTemplateResponse = {
  id: 'template-1',
  domainName: 'Invoice',
  name: 'GB Skips',
  isTenantSpecific: true,
  statuses: [
    { code: 'AWAITING_REVIEW', name: 'Awaiting Review', isTerminal: false, sortOrder: 4 },
    { code: 'CHECKED_READY_TO_APPROVE', name: 'Checked & Ready to Approve', isTerminal: false, sortOrder: 5 },
    { code: 'APPROVED', name: 'Approved', isTerminal: false, sortOrder: 7 },
  ],
  transitions: [],
};

function mockAvailableActions(actions: unknown) {
  vi.mocked(httpClient.get).mockImplementation(async (path) =>
    path === '/api/workflow-template' ? workflowTemplateResponse : actions,
  );
}

beforeEach(() => {
  vi.mocked(httpClient.get).mockReset();
  vi.mocked(httpClient.patch).mockReset();
});

describe('WorkflowActionsPanel', () => {
  it('a FINANCE_MANAGER user sees the Approve action (acceptance criteria)', async () => {
    mockAvailableActions(financeManagerActions);
    renderPanel(['FINANCE_MANAGER'], baseInvoice());

    await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument());
  });

  it('an AP_REVIEWER user does not see the Approve action, but does see Escalate to Febina (acceptance criteria)', async () => {
    mockAvailableActions(apReviewerActions);
    renderPanel(['AP_REVIEWER'], baseInvoice());

    await waitFor(() => expect(screen.getByRole('button', { name: 'Escalate to Febina' })).toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
  });

  it('displays the current status via its tenant-driven display label (task 4)', async () => {
    mockAvailableActions([]);
    renderPanel(['AP_REVIEWER'], baseInvoice({ status: 'AWAITING_REVIEW' }));

    await waitFor(() => expect(screen.getByText('Awaiting Review')).toBeInTheDocument());
  });

  it('requires confirmation before executing an action, and does not call onStatusChanged until confirmed (task 5)', async () => {
    mockAvailableActions(financeManagerActions);
    const onStatusChanged = vi.fn();
    const user = userEvent.setup();
    renderPanel(['FINANCE_MANAGER'], baseInvoice(), onStatusChanged);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'Approve' }));

    expect(screen.getByRole('alertdialog')).toBeInTheDocument();
    expect(onStatusChanged).not.toHaveBeenCalled();
    expect(httpClient.patch).not.toHaveBeenCalled();
  });

  it('cancelling the confirmation dismisses it without executing anything', async () => {
    mockAvailableActions(financeManagerActions);
    const onStatusChanged = vi.fn();
    const user = userEvent.setup();
    renderPanel(['FINANCE_MANAGER'], baseInvoice(), onStatusChanged);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'Approve' }));
    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(onStatusChanged).not.toHaveBeenCalled();
    expect(httpClient.patch).not.toHaveBeenCalled();
  });

  it('switching the pending action without cancelling first resets the note text (WP-084)', async () => {
    // WorkflowActionButtons only disables while isExecuting, not merely
    // while a confirmation is pending - so a second action can be selected
    // before the first is confirmed or cancelled. The dialog must not carry
    // a note typed for one transition over to a different one.
    mockAvailableActions(financeManagerActions);
    const user = userEvent.setup();
    renderPanel(['FINANCE_MANAGER'], baseInvoice());
    await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'Approve' }));
    await user.type(screen.getByLabelText('Note'), 'Approving this one.');
    expect(screen.getByLabelText('Note')).toHaveValue('Approving this one.');

    await user.click(screen.getByRole('button', { name: 'Send Query' }));

    expect(screen.getByRole('alertdialog')).toHaveTextContent(/Send Query/);
    expect(screen.getByLabelText('Note')).toHaveValue('');
    expect(screen.getByRole('button', { name: 'Confirm' })).toBeDisabled();
  });

  it('executes the action after confirmation, shows a success notification, and calls onStatusChanged with the updated invoice merged with the existing pdfUrl (tasks 5, 6, 7)', async () => {
    mockAvailableActions(financeManagerActions);
    vi.mocked(httpClient.patch).mockResolvedValueOnce({
      invoice: {
        id: 'inv-gb-002',
        supplierName: 'Yorkshire Skip Supplies',
        supplierInvoiceNumber: 'YSS-2201',
        invoiceDate: '2026-07-02',
        grossTotal: 640,
        currency: 'GBP',
        status: 'APPROVED',
        isPotentialDuplicate: false,
        duplicateCheckReason: null,
        sourceDocumentBlobName: 'gb-skips/2026/07/inv-gb-002.pdf',
        createdAtUtc: '2026-07-02T07:55:00Z',
      },
      recentAuditEntries: [],
      extractedFields: [],
    });
    const onStatusChanged = vi.fn();
    const user = userEvent.setup();
    const invoice = baseInvoice({ id: 'inv-gb-002' });
    renderPanel(['FINANCE_MANAGER'], invoice, onStatusChanged);
    await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'Approve' }));
    await user.type(screen.getByLabelText('Note'), 'Approved after review.');
    await user.click(screen.getByRole('button', { name: 'Confirm' }));

    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent(/successfully/i));
    expect(onStatusChanged).toHaveBeenCalledTimes(1);
    expect(onStatusChanged).toHaveBeenCalledWith(expect.objectContaining({ status: 'APPROVED', pdfUrl: invoice.pdfUrl }));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
    expect(httpClient.patch).toHaveBeenCalledWith('/api/invoices/inv-gb-002/status', {
      targetStatusCode: 'APPROVED',
      notes: 'Approved after review.',
    });
  });

  it('Confirm is disabled until a note is entered, and the dialog cannot be confirmed with whitespace-only text (WP-084)', async () => {
    mockAvailableActions(financeManagerActions);
    const user = userEvent.setup();
    renderPanel(['FINANCE_MANAGER'], baseInvoice());
    await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'Approve' }));
    const confirmButton = screen.getByRole('button', { name: 'Confirm' });
    expect(confirmButton).toBeDisabled();

    await user.type(screen.getByLabelText('Note'), '   ');
    expect(confirmButton).toBeDisabled();
    expect(httpClient.patch).not.toHaveBeenCalled();
  });

  it('calls onNoteCreated after a successful transition, so the Notes panel can refresh itself (WP-084)', async () => {
    mockAvailableActions(financeManagerActions);
    vi.mocked(httpClient.patch).mockResolvedValueOnce({
      invoice: {
        id: 'inv-gb-002',
        supplierName: 'Yorkshire Skip Supplies',
        supplierInvoiceNumber: 'YSS-2201',
        invoiceDate: '2026-07-02',
        grossTotal: 640,
        currency: 'GBP',
        status: 'APPROVED',
        isPotentialDuplicate: false,
        duplicateCheckReason: null,
        sourceDocumentBlobName: 'gb-skips/2026/07/inv-gb-002.pdf',
        createdAtUtc: '2026-07-02T07:55:00Z',
      },
      recentAuditEntries: [],
      extractedFields: [],
    });
    const onNoteCreated = vi.fn();
    const user = userEvent.setup();
    const authValue: AuthContextValue = {
      user: { tenantId: 'gb-skips', tenantName: 'GB Skips', displayName: 'Test User', roles: ['FINANCE_MANAGER'] },
      isAuthenticated: true,
      signIn: () => {},
      signOut: () => {},
    };
    render(
      <AuthContext.Provider value={authValue}>
        <WorkflowActionsPanel invoice={baseInvoice()} onStatusChanged={vi.fn()} onNoteCreated={onNoteCreated} />
      </AuthContext.Provider>,
    );
    await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument());

    await user.click(screen.getByRole('button', { name: 'Approve' }));
    await user.type(screen.getByLabelText('Note'), 'Approved.');
    await user.click(screen.getByRole('button', { name: 'Confirm' }));

    await waitFor(() => expect(onNoteCreated).toHaveBeenCalledTimes(1));
  });
});
