import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { useInvoiceNavigation } from '@/api/useInvoiceNavigation';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import { httpClient } from '@/api/httpClient';
import type { InvoiceListItem } from '@/types/invoice';

// WP-020: invoiceClient is now HTTP-backed; mock httpClient.get directly
// with a fixed, deterministic ordered page so the navigation math can be
// asserted precisely.
vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn() } };
});

const orderedItems: InvoiceListItem[] = Array.from({ length: 5 }, (_, i) => ({
  id: `inv-${i + 1}`,
  supplierName: `Supplier ${i + 1}`,
  invoiceNumber: `INV-${i + 1}`,
  invoiceDate: `2026-07-0${5 - i}`,
  amount: 100 * (i + 1),
  currencyCode: 'GBP',
  status: 'AWAITING_REVIEW',
  isPotentialDuplicate: false,
  duplicateCheckReason: null,
}));

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
  vi.mocked(httpClient.get).mockResolvedValue({
    items: orderedItems,
    totalCount: orderedItems.length,
    page: 1,
    pageSize: 1000,
  });
});

describe('useInvoiceNavigation', () => {
  it('returns previous/next ids and position matching the mocked order', async () => {
    const middleId = orderedItems[2].id;

    const { result } = renderHook(() => useInvoiceNavigation(middleId), { wrapper });

    await waitFor(() => expect(result.current.total).toBe(orderedItems.length));

    expect(result.current.position).toBe(3);
    expect(result.current.previousId).toBe(orderedItems[1].id);
    expect(result.current.nextId).toBe(orderedItems[3].id);
  });

  it('returns a null previousId for the first invoice in the order', async () => {
    const { result } = renderHook(() => useInvoiceNavigation(orderedItems[0].id), { wrapper });

    await waitFor(() => expect(result.current.total).toBe(orderedItems.length));
    expect(result.current.previousId).toBeNull();
    expect(result.current.nextId).not.toBeNull();
  });

  it('returns a null nextId for the last invoice in the order', async () => {
    const lastId = orderedItems[orderedItems.length - 1].id;

    const { result } = renderHook(() => useInvoiceNavigation(lastId), { wrapper });

    await waitFor(() => expect(result.current.total).toBe(orderedItems.length));
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
