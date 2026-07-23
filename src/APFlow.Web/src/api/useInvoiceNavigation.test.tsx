import { describe, expect, it } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { useInvoiceNavigation } from '@/api/useInvoiceNavigation';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import { invoiceClient } from '@/api/invoiceClient';

const authValue: AuthContextValue = {
  user: { tenantId: 'platform-default', tenantName: 'Platform Default Tenant', displayName: 'Test User' },
  isAuthenticated: true,
  signIn: () => {},
  signOut: () => {},
};

function wrapper({ children }: { children: ReactNode }) {
  return <AuthContext.Provider value={authValue}>{children}</AuthContext.Provider>;
}

describe('useInvoiceNavigation', () => {
  it('returns previous/next ids and position matching the invoiceClient default order', async () => {
    const fullOrder = await invoiceClient.queryInvoices({
      tenantId: 'platform-default',
      sortBy: 'invoiceDate',
      sortDirection: 'desc',
      page: 1,
      pageSize: 1000,
    });
    const middleId = fullOrder.items[Math.floor(fullOrder.items.length / 2)].id;
    const expectedIndex = fullOrder.items.findIndex((item) => item.id === middleId);

    const { result } = renderHook(() => useInvoiceNavigation(middleId), { wrapper });

    await waitFor(() => expect(result.current.total).toBe(fullOrder.totalCount));

    expect(result.current.position).toBe(expectedIndex + 1);
    expect(result.current.previousId).toBe(fullOrder.items[expectedIndex - 1].id);
    expect(result.current.nextId).toBe(fullOrder.items[expectedIndex + 1].id);
  });

  it('returns a null previousId for the first invoice in the order', async () => {
    const fullOrder = await invoiceClient.queryInvoices({
      tenantId: 'platform-default',
      sortBy: 'invoiceDate',
      sortDirection: 'desc',
      page: 1,
      pageSize: 1000,
    });
    const firstId = fullOrder.items[0].id;

    const { result } = renderHook(() => useInvoiceNavigation(firstId), { wrapper });

    await waitFor(() => expect(result.current.total).toBe(fullOrder.totalCount));
    expect(result.current.previousId).toBeNull();
    expect(result.current.nextId).not.toBeNull();
  });

  it('returns a null nextId for the last invoice in the order', async () => {
    const fullOrder = await invoiceClient.queryInvoices({
      tenantId: 'platform-default',
      sortBy: 'invoiceDate',
      sortDirection: 'desc',
      page: 1,
      pageSize: 1000,
    });
    const lastId = fullOrder.items[fullOrder.items.length - 1].id;

    const { result } = renderHook(() => useInvoiceNavigation(lastId), { wrapper });

    await waitFor(() => expect(result.current.total).toBe(fullOrder.totalCount));
    expect(result.current.nextId).toBeNull();
    expect(result.current.previousId).not.toBeNull();
  });

  it('returns nulls when there is no current invoice id', async () => {
    const { result } = renderHook(() => useInvoiceNavigation(undefined), { wrapper });
    await act(async () => {
      await Promise.resolve();
    });

    expect(result.current.previousId).toBeNull();
    expect(result.current.nextId).toBeNull();
    expect(result.current.position).toBeNull();
  });
});
