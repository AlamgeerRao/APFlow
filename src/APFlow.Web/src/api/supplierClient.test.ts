import { describe, expect, it, vi, beforeEach } from 'vitest';
import { FixtureSupplierClient, HttpSupplierClient } from '@/api/supplierClient';
import { supplierFixtures } from '@/api/fixtures/suppliers.fixture';
import { httpClient } from '@/api/httpClient';
import type { SaveSupplierRequest } from '@/types/supplier';

vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn(), post: vi.fn(), put: vi.fn() } };
});

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

describe('FixtureSupplierClient', () => {
  it('returns every fixture supplier, alphabetically by name', async () => {
    const client = new FixtureSupplierClient();

    const result = await client.getAll();

    expect(result).toHaveLength(supplierFixtures.length);
    expect(result.map((s) => s.name)).toEqual([...result.map((s) => s.name)].sort((a, b) => a.localeCompare(b)));
  });

  it('creates a supplier and reflects it in a subsequent getAll', async () => {
    const client = new FixtureSupplierClient();

    const created = await client.create(baseRequest);

    expect(created.name).toBe('New Vendor Ltd');
    expect(created.id).toBeTruthy();
    const all = await client.getAll();
    expect(all.map((s) => s.id)).toContain(created.id);
  });

  it('rejects creating with a non-null credit limit when the caller is not FINANCE_MANAGER', async () => {
    const client = new FixtureSupplierClient(() => false);

    await expect(client.create({ ...baseRequest, creditLimit: 1000 })).rejects.toThrow(/FINANCE_MANAGER/);
  });

  it('allows creating with a non-null credit limit when the caller is FINANCE_MANAGER', async () => {
    const client = new FixtureSupplierClient(() => true);

    const created = await client.create({ ...baseRequest, creditLimit: 1000 });

    expect(created.creditLimit).toBe(1000);
  });

  it('updates a supplier when every other field changes but credit limit is round-tripped unchanged, even for a non-FINANCE_MANAGER caller', async () => {
    const client = new FixtureSupplierClient(() => false);
    const existing = (await client.getAll())[0];

    const updated = await client.update(existing.id, { ...baseRequest, name: 'Renamed Ltd', creditLimit: existing.creditLimit });

    expect(updated.name).toBe('Renamed Ltd');
    expect(updated.creditLimit).toBe(existing.creditLimit);
  });

  it('rejects updating with a changed credit limit when the caller is not FINANCE_MANAGER', async () => {
    const client = new FixtureSupplierClient(() => false);
    const existing = (await client.getAll())[0];

    await expect(
      client.update(existing.id, { ...baseRequest, creditLimit: (existing.creditLimit ?? 0) + 500 }),
    ).rejects.toThrow(/FINANCE_MANAGER/);
  });

  it('rejects updating a supplier id that does not exist', async () => {
    const client = new FixtureSupplierClient();

    await expect(client.update('not-a-real-id', baseRequest)).rejects.toThrow(/not found/i);
  });
});

describe('HttpSupplierClient', () => {
  beforeEach(() => {
    vi.mocked(httpClient.get).mockReset();
    vi.mocked(httpClient.post).mockReset();
    vi.mocked(httpClient.put).mockReset();
  });

  it('calls GET /api/suppliers for getAll', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce([]);
    const client = new HttpSupplierClient();

    await client.getAll();

    expect(httpClient.get).toHaveBeenCalledWith('/api/suppliers');
  });

  it('calls POST /api/suppliers with the request body for create, and returns the real SupplierDto field shape', async () => {
    const dto = {
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
    vi.mocked(httpClient.post).mockResolvedValueOnce(dto);
    const client = new HttpSupplierClient();

    const result = await client.create(baseRequest);

    expect(httpClient.post).toHaveBeenCalledWith('/api/suppliers', baseRequest);
    expect(result).toEqual(dto);
  });

  it('calls PUT /api/suppliers/{id} with the request body for update', async () => {
    const dto = { ...baseRequest, id: 'supplier-001', createdAtUtc: '2026-01-10T09:00:00Z' };
    vi.mocked(httpClient.put).mockResolvedValueOnce(dto);
    const client = new HttpSupplierClient();

    await client.update('supplier-001', baseRequest);

    expect(httpClient.put).toHaveBeenCalledWith('/api/suppliers/supplier-001', baseRequest);
  });
});
