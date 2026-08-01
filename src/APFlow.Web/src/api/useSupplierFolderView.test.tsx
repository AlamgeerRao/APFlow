import { describe, expect, it, vi } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { useSupplierFolderView } from '@/api/useSupplierFolderView';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import { httpClient } from '@/api/httpClient';
import { FixtureSupplierFolderClient } from '@/api/supplierFolderClient';
import type { InvoiceListItem } from '@/types/invoice';

// WP-065: supplierFolderClient is now HTTP-backed. This suite tests the
// hook's own plumbing (URL sync, debounce, pagination state), not the real
// backend, so httpClient.get is mocked to delegate to
// FixtureSupplierFolderClient's known-good filtering logic, translated into
// the real endpoints' response shapes (see HttpSupplierFolderClient's own
// field mapping), giving realistic filtered data without a live API.
vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn() } };
});

const TENANT_ID = 'gb-skips';
const fixtureClient = new FixtureSupplierFolderClient();

function toResponseItem(invoice: InvoiceListItem) {
  return {
    id: invoice.id,
    supplierName: invoice.supplierName,
    supplierInvoiceNumber: invoice.invoiceNumber,
    invoiceDate: invoice.invoiceDate,
    grossTotal: invoice.amount,
    currency: invoice.currencyCode,
    status: invoice.status,
    isPotentialDuplicate: invoice.isPotentialDuplicate,
    duplicateCheckReason: invoice.duplicateCheckReason,
  };
}

vi.mocked(httpClient.get).mockImplementation(async (path: string, options?: unknown) => {
  const params = ((options as { params?: Record<string, string | number | undefined> } | undefined)?.params ??
    {}) as Record<string, string | number | undefined>;

  if (path === '/api/invoices/folders') {
    return fixtureClient.getFolderCounts(TENANT_ID, params.search as string | undefined);
  }
  if (path === '/api/invoices/suppliers') {
    return fixtureClient.getSuppliers(
      TENANT_ID,
      params.folder as string | undefined,
      params.search as string | undefined,
    );
  }
  if (path === '/api/invoices/grouped') {
    const result = await fixtureClient.getGroupedInvoices({
      tenantId: TENANT_ID,
      folder: params.folder as string | undefined,
      supplier: params.supplier as string | undefined,
      search: params.search as string | undefined,
      page: Number(params.page ?? 1),
      pageSize: Number(params.pageSize ?? 5),
    });
    return {
      groups: result.groups.map((g) => ({
        supplierName: g.supplierName,
        count: g.count,
        invoices: g.invoices.map(toResponseItem),
      })),
      totalSuppliers: result.totalSuppliers,
      page: result.page,
      pageSize: result.pageSize,
    };
  }
  throw new Error(`Unexpected path in test: ${path}`);
});

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
