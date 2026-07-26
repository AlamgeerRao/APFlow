import { describe, expect, it } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { useSupplierFolderView } from '@/api/useSupplierFolderView';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';

const authValue: AuthContextValue = {
  user: { tenantId: 'gb-skips', tenantName: 'GB Skips', displayName: 'Test User', roles: ['AP_REVIEWER'] },
  isAuthenticated: true,
  signIn: () => {},
  signOut: () => {},
};

function makeWrapper(initialEntries: string[]) {
  return function Wrapper({ children }: { children: ReactNode }) {
    return (
      <MemoryRouter initialEntries={initialEntries}>
        <AuthContext.Provider value={authValue}>{children}</AuthContext.Provider>
      </MemoryRouter>
    );
  };
}

describe('useSupplierFolderView', () => {
  it('loads folder counts, supplier options, and grouped results for the acting tenant', async () => {
    const { result } = renderHook(() => useSupplierFolderView(), { wrapper: makeWrapper(['/suppliers']) });

    await waitFor(() => expect(result.current.isLoading).toBe(false));

    expect(result.current.folderCounts.length).toBeGreaterThan(0);
    expect(result.current.supplierOptions.length).toBeGreaterThan(0);
    expect(result.current.result?.groups.length).toBeGreaterThan(0);
  });

  it('reads the initial folder/supplier/search/page from the URL', async () => {
    const { result } = renderHook(() => useSupplierFolderView(), {
      wrapper: makeWrapper(['/suppliers?folder=AWAITING_REVIEW&supplier=Dales+Aggregates&page=1']),
    });

    expect(result.current.folder).toBe('AWAITING_REVIEW');
    expect(result.current.supplier).toBe('Dales Aggregates');

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.result?.groups.every((g) => g.supplierName === 'Dales Aggregates')).toBe(true);
  });

  it('resets to page 1 when the folder changes', async () => {
    const { result } = renderHook(() => useSupplierFolderView(), {
      wrapper: makeWrapper(['/suppliers?page=2']),
    });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => result.current.setFolder('AWAITING_REVIEW'));

    await waitFor(() => expect(result.current.page).toBe(1));
    expect(result.current.folder).toBe('AWAITING_REVIEW');
  });

  it('narrows results to the selected folder', async () => {
    const { result } = renderHook(() => useSupplierFolderView(), { wrapper: makeWrapper(['/suppliers']) });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    act(() => result.current.setFolder('CHECKED_READY_TO_APPROVE'));
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const allInvoices = result.current.result?.groups.flatMap((g) => g.invoices) ?? [];
    expect(allInvoices.length).toBeGreaterThan(0);
    expect(allInvoices.every((i) => i.status === 'CHECKED_READY_TO_APPROVE')).toBe(true);
  });
});
