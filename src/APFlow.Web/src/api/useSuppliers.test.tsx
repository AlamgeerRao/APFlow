import { describe, expect, it, vi, beforeEach } from 'vitest';
import { act, renderHook, waitFor } from '@testing-library/react';
import type { ReactNode } from 'react';
import { useSuppliers } from '@/api/useSuppliers';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import { httpClient, ApiError } from '@/api/httpClient';
import type { SaveSupplierRequest } from '@/types/supplier';

vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn(), post: vi.fn(), put: vi.fn() } };
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

const baseRequest: SaveSupplierRequest = {
  name: 'New Vendor Ltd',
  code: null,
  email: null,
  phone: null,
  creditLimit: null,
  paymentTermsDays: null,
  accountingReference: null,
  status: 'ACTIVE',
};

const supplierDto = {
  id: 'supplier-999',
  name: 'New Vendor Ltd',
  code: null,
  email: null,
  phone: null,
  creditLimit: null,
  paymentTermsDays: null,
  accountingReference: null,
  status: 'ACTIVE',
  createdAtUtc: '2026-08-07T09:00:00Z',
};

beforeEach(() => {
  vi.mocked(httpClient.get).mockReset();
  vi.mocked(httpClient.post).mockReset();
  vi.mocked(httpClient.put).mockReset();
});

describe('useSuppliers', () => {
  it('loads the supplier list on mount', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce([supplierDto]);

    const { result } = renderHook(() => useSuppliers(), { wrapper: wrapperFor(['AP_REVIEWER']) });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.suppliers).toEqual([supplierDto]);
    expect(result.current.error).toBeNull();
  });

  it('sets an error message when the initial load fails', async () => {
    vi.mocked(httpClient.get).mockRejectedValueOnce(new ApiError(500, 'boom'));

    const { result } = renderHook(() => useSuppliers(), { wrapper: wrapperFor(['AP_REVIEWER']) });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.error).toMatch(/Unable to load suppliers/);
  });

  it('createSupplier posts the request, reloads the list, and returns the created supplier', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce([]).mockResolvedValueOnce([supplierDto]);
    vi.mocked(httpClient.post).mockResolvedValueOnce(supplierDto);

    const { result } = renderHook(() => useSuppliers(), { wrapper: wrapperFor(['AP_REVIEWER']) });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const created = await act(async () => result.current.createSupplier(baseRequest));

    expect(created).toEqual(supplierDto);
    expect(httpClient.post).toHaveBeenCalledWith('/api/suppliers', baseRequest);
    await waitFor(() => expect(result.current.suppliers).toEqual([supplierDto]));
    expect(result.current.submitError).toBeNull();
  });

  it('updateSupplier puts the request and returns the updated supplier', async () => {
    vi.mocked(httpClient.get).mockResolvedValue([supplierDto]);
    vi.mocked(httpClient.put).mockResolvedValueOnce({ ...supplierDto, name: 'Renamed Ltd' });

    const { result } = renderHook(() => useSuppliers(), { wrapper: wrapperFor(['FINANCE_MANAGER']) });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const updated = await act(async () =>
      result.current.updateSupplier('supplier-999', { ...baseRequest, name: 'Renamed Ltd' }),
    );

    expect(updated?.name).toBe('Renamed Ltd');
    expect(httpClient.put).toHaveBeenCalledWith('/api/suppliers/supplier-999', { ...baseRequest, name: 'Renamed Ltd' });
  });

  it('surfaces a real 403 Supplier.CreditLimitForbidden without crashing, and flags submitErrorIsCreditLimitForbidden', async () => {
    vi.mocked(httpClient.get).mockResolvedValue([supplierDto]);
    vi.mocked(httpClient.put).mockRejectedValueOnce(
      new ApiError(403, "Changing a supplier's credit limit requires the 'FINANCE_MANAGER' role.", 'Supplier.CreditLimitForbidden'),
    );

    const { result } = renderHook(() => useSuppliers(), { wrapper: wrapperFor(['AP_REVIEWER']) });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    const updated = await act(async () =>
      result.current.updateSupplier('supplier-999', { ...baseRequest, creditLimit: 5000 }),
    );

    expect(updated).toBeNull();
    expect(result.current.submitError).toMatch(/FINANCE_MANAGER/);
    expect(result.current.submitErrorIsCreditLimitForbidden).toBe(true);
  });

  it('does not set submitErrorIsCreditLimitForbidden for an unrelated failure', async () => {
    vi.mocked(httpClient.get).mockResolvedValue([supplierDto]);
    vi.mocked(httpClient.post).mockRejectedValueOnce(new ApiError(400, 'Supplier name must not be empty.', 'Supplier.InvalidName'));

    const { result } = renderHook(() => useSuppliers(), { wrapper: wrapperFor(['AP_REVIEWER']) });
    await waitFor(() => expect(result.current.isLoading).toBe(false));

    await act(async () => result.current.createSupplier({ ...baseRequest, name: '' }));

    expect(result.current.submitErrorIsCreditLimitForbidden).toBe(false);
    expect(result.current.submitError).toMatch(/name/);
  });
});
