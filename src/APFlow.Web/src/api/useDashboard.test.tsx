import { describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { useDashboard } from '@/api/useDashboard';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import { httpClient } from '@/api/httpClient';
import { FixtureSupplierFolderClient } from '@/api/supplierFolderClient';
import { FixtureDashboardClient } from '@/api/dashboardClient';

// Same reasoning as useSupplierFolderView.test.tsx: both real clients are
// HTTP-backed, so httpClient.get is mocked to delegate to the known-good
// fixture implementations, giving this suite realistic data without a live
// backend while testing only this hook's own plumbing (loading/error/retry,
// combining two independent client calls).
vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn() } };
});

const TENANT_ID = 'gb-skips';
const folderFixtureClient = new FixtureSupplierFolderClient();
const dashboardFixtureClient = new FixtureDashboardClient();

vi.mocked(httpClient.get).mockImplementation(async (path: string) => {
  if (path === '/api/invoices/folders') {
    return folderFixtureClient.getFolderCounts(TENANT_ID);
  }
  if (path === '/api/invoices') {
    const items = await dashboardFixtureClient.getRecentActivity(TENANT_ID, 8);
    return {
      items: items.map((item) => ({
        id: item.id,
        supplierName: item.supplierName,
        supplierInvoiceNumber: item.invoiceNumber,
        status: item.status,
        createdAtUtc: item.createdAtUtc,
      })),
    };
  }
  throw new Error(`Unexpected path in test: ${path}`);
});

const authValue: AuthContextValue = {
  user: { tenantId: TENANT_ID, tenantName: 'GB Skips', displayName: 'Test User', roles: ['AP_REVIEWER'] },
  isAuthenticated: true,
  signIn: () => {},
  signOut: () => {},
};

function Wrapper({ children }: { children: ReactNode }) {
  return <AuthContext.Provider value={authValue}>{children}</AuthContext.Provider>;
}

describe('useDashboard', () => {
  it('loads folder counts and recent activity together', async () => {
    const { result } = renderHook(() => useDashboard(), { wrapper: Wrapper });

    expect(result.current.isLoading).toBe(true);

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.folderCounts.length).toBeGreaterThan(0);
    expect(result.current.recentActivity.length).toBeGreaterThan(0);
    expect(result.current.error).toBeNull();
  });

  it('surfaces a single error and stops loading if either call fails', async () => {
    vi.mocked(httpClient.get).mockImplementationOnce(async () => {
      throw new Error('network error');
    });

    const { result } = renderHook(() => useDashboard(), { wrapper: Wrapper });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.error).toBe('Unable to load dashboard data. Please try again.');
  });

  it('retry re-queries both data sources', async () => {
    const { result } = renderHook(() => useDashboard(), { wrapper: Wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const callsBefore = vi.mocked(httpClient.get).mock.calls.length;
    act(() => result.current.retry());

    await waitFor(() => expect(vi.mocked(httpClient.get).mock.calls.length).toBeGreaterThan(callsBefore));
  });

  it('does not query when no tenant is available yet', () => {
    const noUserAuthValue: AuthContextValue = { ...authValue, user: null };
    function NoUserWrapper({ children }: { children: ReactNode }) {
      return <AuthContext.Provider value={noUserAuthValue}>{children}</AuthContext.Provider>;
    }

    const { result } = renderHook(() => useDashboard(), { wrapper: NoUserWrapper });

    expect(result.current.isLoading).toBe(false);
    expect(result.current.folderCounts).toEqual([]);
    expect(result.current.recentActivity).toEqual([]);
  });
});
