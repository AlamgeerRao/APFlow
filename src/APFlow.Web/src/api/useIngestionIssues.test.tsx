import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { useIngestionIssues } from '@/api/useIngestionIssues';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import { httpClient } from '@/api/httpClient';

vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn() } };
});

const authValue: AuthContextValue = {
  user: { tenantId: 'platform-default', tenantName: 'Platform Default Tenant', displayName: 'Test User', roles: ['AP_REVIEWER'] },
  isAuthenticated: true,
  signIn: () => {},
  signOut: () => {},
};

function wrapper({ children }: { children: ReactNode }) {
  return <AuthContext.Provider value={authValue}>{children}</AuthContext.Provider>;
}

beforeEach(() => {
  vi.mocked(httpClient.get).mockReset();
});

describe('useIngestionIssues', () => {
  it('loads the first page for the acting tenant', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({
      items: [
        {
          id: 'issue-1',
          senderAddress: 'vendor@example.com',
          senderName: 'Vendor Co',
          subject: 'No invoice here',
          firstSeenUtc: '2026-07-20T09:12:00Z',
          lastSeenUtc: '2026-07-20T09:12:00Z',
          occurrenceCount: 1,
          reasonCode: 'NO_PROCESSABLE_ATTACHMENTS',
          attachmentsFound: '(none)',
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    });

    const { result } = renderHook(() => useIngestionIssues(), { wrapper });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(httpClient.get).toHaveBeenCalledWith('/api/ingestion-issues', { params: { page: 1, pageSize: 25 } });
    expect(result.current.result?.items).toHaveLength(1);
    expect(result.current.error).toBeNull();
  });

  it('re-queries with the new page when setPage is called', async () => {
    vi.mocked(httpClient.get)
      .mockResolvedValueOnce({ items: [], totalCount: 30, page: 1, pageSize: 25 })
      .mockResolvedValueOnce({ items: [], totalCount: 30, page: 2, pageSize: 25 });

    const { result } = renderHook(() => useIngestionIssues(), { wrapper });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => result.current.setPage(2));

    await waitFor(() => expect(httpClient.get).toHaveBeenCalledTimes(2));
    expect(httpClient.get).toHaveBeenLastCalledWith('/api/ingestion-issues', { params: { page: 2, pageSize: 25 } });
  });

  it('reports an error and empty state when the request fails', async () => {
    vi.mocked(httpClient.get).mockRejectedValueOnce(new Error('boom'));

    const { result } = renderHook(() => useIngestionIssues(), { wrapper });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.error).toMatch(/unable to load/i);
    expect(result.current.result).toBeNull();
  });
});
