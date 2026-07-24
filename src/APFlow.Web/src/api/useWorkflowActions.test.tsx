import { describe, expect, it } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { useWorkflowActions } from '@/api/useWorkflowActions';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';

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

describe('useWorkflowActions', () => {
  it('loads the actions available for the given status and role (task 3)', async () => {
    const { result } = renderHook(() => useWorkflowActions('inv-gb-002', 'CHECKED_READY_TO_APPROVE'), {
      wrapper: wrapperFor(['FINANCE_MANAGER']),
    });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.actions.map((a) => a.targetStatusLabel)).toContain('Approve');
  });

  it('excludes Approve for an AP_REVIEWER on the same status', async () => {
    const { result } = renderHook(() => useWorkflowActions('inv-gb-002', 'CHECKED_READY_TO_APPROVE'), {
      wrapper: wrapperFor(['AP_REVIEWER']),
    });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.actions.map((a) => a.targetStatusLabel)).not.toContain('Approve');
  });

  it('re-fetches when fromStatusCode changes, reflecting the new status\'s own actions', async () => {
    const { result, rerender } = renderHook(
      ({ status }) => useWorkflowActions('inv-gb-005', status),
      { wrapper: wrapperFor(['AP_REVIEWER']), initialProps: { status: 'AWAITING_REVIEW' } },
    );

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.actions.map((a) => a.targetStatusCode)).toContain('CHECKED_READY_TO_APPROVE');

    rerender({ status: 'NEEDS_REVIEW_FEBINA' });

    await waitFor(() => expect(result.current.actions.map((a) => a.targetStatusCode)).toContain('NEEDS_QUERY'));
  });

  it('executeAction returns true on success', async () => {
    const { result } = renderHook(() => useWorkflowActions('inv-gb-001', 'AWAITING_REVIEW'), {
      wrapper: wrapperFor(['AP_REVIEWER']),
    });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const succeeded = await act(async () =>
      result.current.executeAction({ targetStatusCode: 'CHECKED_READY_TO_APPROVE', targetStatusLabel: 'Mark Checked & Ready to Approve' }),
    );

    expect(succeeded).toBe(true);
    expect(result.current.executeError).toBeNull();
  });

  it('executeAction returns false and sets executeError for an unpermitted action', async () => {
    const { result } = renderHook(() => useWorkflowActions('inv-gb-002', 'CHECKED_READY_TO_APPROVE'), {
      wrapper: wrapperFor(['AP_REVIEWER']),
    });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const succeeded = await act(async () => result.current.executeAction({ targetStatusCode: 'APPROVED', targetStatusLabel: 'Approve' }));

    expect(succeeded).toBe(false);
    expect(result.current.executeError).toMatch(/Finance Manager/);
  });
});
