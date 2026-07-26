import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { useWorkflowActions } from '@/api/useWorkflowActions';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import { httpClient, ApiError } from '@/api/httpClient';

// WP-020: role-gating/transition-graph enforcement now lives server-side
// (WP-053/054), not in this fixture. This suite mocks httpClient directly
// and tests the hook's own plumbing (loading state, re-fetch on status
// change, success/error handling) rather than business rules that no
// longer exist client-side.
vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn(), patch: vi.fn() } };
});

function wrapperFor(roles: string[]) {
  const authValue: AuthContextValue = {
    user: { tenantId: 'gb-skips', tenantName: 'GB Skips', displayName: 'Test User', roles },
    isAuthenticated: true,
    signIn: () => {},
    signOut: () => {},
  };
  return function wrapper({ children }: { children: ReactNode }) {
    return <AuthContext.Provider value={authValue}>{children}</AuthContext.Provider>;
  };
}

const detailResponseFixture = {
  invoice: {
    id: 'inv-gb-002',
    supplierName: 'Yorkshire Skip Supplies',
    supplierInvoiceNumber: 'YSS-2202',
    invoiceDate: '2026-07-05',
    grossTotal: 640,
    currency: 'GBP',
    status: 'APPROVED',
    isPotentialDuplicate: false,
    duplicateCheckReason: null,
    sourceDocumentBlobName: 'gb-skips/2026/07/inv-gb-002.pdf',
    createdAtUtc: '2026-07-05T10:05:00Z',
  },
  recentAuditEntries: [],
  extractedFields: [],
};

beforeEach(() => {
  vi.mocked(httpClient.get).mockReset();
  vi.mocked(httpClient.patch).mockReset();
});

describe('useWorkflowActions', () => {
  it('requests available actions for the given invoice and applies the response', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce([
      { targetStatusCode: 'APPROVED', targetStatusLabel: 'Approve' },
    ]);

    const { result } = renderHook(() => useWorkflowActions('inv-gb-002', 'CHECKED_READY_TO_APPROVE'), {
      wrapper: wrapperFor(['FINANCE_MANAGER']),
    });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(httpClient.get).toHaveBeenCalledWith('/api/invoices/inv-gb-002/available-actions');
    expect(result.current.actions.map((a) => a.targetStatusLabel)).toContain('Approve');
  });

  it('re-fetches when fromStatusCode changes', async () => {
    vi.mocked(httpClient.get)
      .mockResolvedValueOnce([{ targetStatusCode: 'CHECKED_READY_TO_APPROVE', targetStatusLabel: 'Mark Ready' }])
      .mockResolvedValueOnce([{ targetStatusCode: 'NEEDS_QUERY', targetStatusLabel: 'Send Query' }]);

    const { result, rerender } = renderHook(({ status }) => useWorkflowActions('inv-gb-005', status), {
      wrapper: wrapperFor(['AP_REVIEWER']),
      initialProps: { status: 'AWAITING_REVIEW' },
    });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.actions.map((a) => a.targetStatusCode)).toContain('CHECKED_READY_TO_APPROVE');

    rerender({ status: 'NEEDS_REVIEW_FEBINA' });

    await waitFor(() => expect(result.current.actions.map((a) => a.targetStatusCode)).toContain('NEEDS_QUERY'));
    expect(httpClient.get).toHaveBeenCalledTimes(2);
  });

  it('executeAction returns the updated invoice detail on success', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce([]);
    vi.mocked(httpClient.patch).mockResolvedValueOnce(detailResponseFixture);

    const { result } = renderHook(() => useWorkflowActions('inv-gb-002', 'CHECKED_READY_TO_APPROVE'), {
      wrapper: wrapperFor(['FINANCE_MANAGER']),
    });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const updated = await act(async () =>
      result.current.executeAction({ targetStatusCode: 'APPROVED', targetStatusLabel: 'Approve' }),
    );

    expect(updated?.status).toBe('APPROVED');
    expect(updated).not.toHaveProperty('pdfUrl');
    expect(httpClient.patch).toHaveBeenCalledWith('/api/invoices/inv-gb-002/status', {
      targetStatusCode: 'APPROVED',
      notes: null,
    });
    expect(result.current.executeError).toBeNull();
  });

  it('executeAction returns null and sets executeError from a 403 ApiError', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce([]);
    vi.mocked(httpClient.patch).mockRejectedValueOnce(
      new ApiError(403, 'The "Approve" action requires the Finance Manager / Decision-Maker role.', 'Workflow.RoleNotPermitted'),
    );

    const { result } = renderHook(() => useWorkflowActions('inv-gb-002', 'CHECKED_READY_TO_APPROVE'), {
      wrapper: wrapperFor(['AP_REVIEWER']),
    });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const updated = await act(async () =>
      result.current.executeAction({ targetStatusCode: 'APPROVED', targetStatusLabel: 'Approve' }),
    );

    expect(updated).toBeNull();
    expect(result.current.executeError).toMatch(/Finance Manager/);
  });
});
