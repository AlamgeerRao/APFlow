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

beforeEach(() => {
  vi.mocked(httpClient.get).mockReset();
  vi.mocked(httpClient.patch).mockReset();
});

describe('WorkflowActionsPanel', () => {
  it('a FINANCE_MANAGER user sees the Approve action (acceptance criteria)', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce(financeManagerActions);
    renderPanel(['FINANCE_MANAGER'], baseInvoice());

    await waitFor(() => expect(screen.getByRole('button', { name: 'Approve' })).toBeInTheDocument());
  });

  it('an AP_REVIEWER user does not see the Approve action, but does see Escalate to Febina (acceptance criteria)', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce(apReviewerActions);
    renderPanel(['AP_REVIEWER'], baseInvoice());

    await waitFor(() => expect(screen.getByRole('button', { name: 'Escalate to Febina' })).toBeInTheDocument());
    expect(screen.queryByRole('button', { name: 'Approve' })).not.toBeInTheDocument();
  });

  it('displays the current status via its tenant-driven display label (task 4)', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce([]);
    renderPanel(['AP_REVIEWER'], baseInvoice({ status: 'AWAITING_REVIEW' }));

    await waitFor(() => expect(screen.getByText('Awaiting Review')).toBeInTheDocument());
  });

  it('requires confirmation before executing an action, and does not call onStatusChanged until confirmed (task 5)', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce(financeManagerActions);
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
    vi.mocked(httpClient.get).mockResolvedValueOnce(financeManagerActions);
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

  it('executes the action after confirmation, shows a success notification, and calls onStatusChanged with the updated invoice merged with the existing pdfUrl (tasks 5, 6, 7)', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce(financeManagerActions);
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
    await user.click(screen.getByRole('button', { name: 'Confirm' }));

    await waitFor(() => expect(screen.getByRole('status')).toHaveTextContent(/successfully/i));
    expect(onStatusChanged).toHaveBeenCalledTimes(1);
    expect(onStatusChanged).toHaveBeenCalledWith(expect.objectContaining({ status: 'APPROVED', pdfUrl: invoice.pdfUrl }));
    expect(screen.queryByRole('alertdialog')).not.toBeInTheDocument();
  });
});
