import type { SaveSupplierRequest, Supplier } from '@/types/supplier';
import { supplierFixtures } from '@/api/fixtures/suppliers.fixture';
import { httpClient } from '@/api/httpClient';

/**
 * Client-side contract for WP-027's Supplier Management UI, calling
 * WP-026's real `api/suppliers` CRUD endpoints. Consumers (`useSuppliers`)
 * depend on this interface, not the fixture implementation below — same
 * pattern as every other client in this codebase (see
 * `supplierFolderClient.ts`).
 *
 * Deliberately only `getAll`/`create`/`update` — no `getById`/`delete`.
 * `getById` has no caller: the UI resolves the supplier to edit from the
 * list `getAll` already returned (via `SuppliersPage`'s
 * name-to-supplier map), never a standalone lookup. `delete` has no UI
 * this WP builds (Sprint2-Plan.md §3 WP-027's task list is create/edit
 * only) — the real endpoint exists server-side (WP-026) for whenever a
 * later WP needs it, but this client only exposes what's actually
 * consumed, matching `ingestionIssueClient.ts`'s own minimal-surface
 * precedent.
 */
export interface SupplierClient {
  /** Every supplier visible to the current tenant. */
  getAll(): Promise<Supplier[]>;
  /** Creates a new supplier. Rejects (throws) on validation failure or (WP-026 task 5) a 403 Supplier.CreditLimitForbidden. */
  create(request: SaveSupplierRequest): Promise<Supplier>;
  /** Full-field-replace update. Rejects (throws) on validation failure, 404, or a 403 Supplier.CreditLimitForbidden. */
  update(id: string, request: SaveSupplierRequest): Promise<Supplier>;
}

/**
 * Fixture-backed implementation, holding suppliers in memory (module scope,
 * same lifetime convention as every other mutating fixture client in this
 * codebase, e.g. `FixtureInvoiceNoteClient`). Kept only for its own unit
 * test coverage — see `HttpSupplierClient` below for what the app actually
 * uses. Mirrors the real backend's Credit Limit permission split (WP-026
 * task 5) so its error path matches what the real API would reject with.
 */
export class FixtureSupplierClient implements SupplierClient {
  private suppliers: Supplier[] = structuredClone(supplierFixtures);

  constructor(private readonly callerHoldsFinanceManagerRole: () => boolean = () => false) {}

  async getAll(): Promise<Supplier[]> {
    return [...this.suppliers].sort((a, b) => a.name.localeCompare(b.name));
  }

  async create(request: SaveSupplierRequest): Promise<Supplier> {
    if (request.creditLimit !== null && !this.callerHoldsFinanceManagerRole()) {
      throw creditLimitForbiddenError();
    }

    const supplier: Supplier = {
      id: crypto.randomUUID(),
      name: request.name,
      code: request.code,
      email: request.email,
      phone: request.phone,
      creditLimit: request.creditLimit,
      paymentTermsDays: request.paymentTermsDays,
      accountingReference: request.accountingReference,
      status: request.status,
      createdAtUtc: new Date().toISOString(),
    };
    this.suppliers = [...this.suppliers, supplier];
    return supplier;
  }

  async update(id: string, request: SaveSupplierRequest): Promise<Supplier> {
    const existing = this.suppliers.find((supplier) => supplier.id === id);
    if (!existing) {
      throw new Error(`Supplier '${id}' was not found.`);
    }

    if (request.creditLimit !== existing.creditLimit && !this.callerHoldsFinanceManagerRole()) {
      throw creditLimitForbiddenError();
    }

    const updated: Supplier = { ...existing, ...request };
    this.suppliers = this.suppliers.map((supplier) => (supplier.id === id ? updated : supplier));
    return updated;
  }
}

function creditLimitForbiddenError(): Error {
  return new Error("Changing a supplier's credit limit requires the 'FINANCE_MANAGER' role.");
}

/**
 * Real HTTP-backed implementation, calling WP-026's `GET/POST/PUT
 * api/suppliers`. Field names confirmed directly against
 * `SupplierDto`/`SaveSupplierRequest`
 * (`src/APFlow.Application/DTOs/SupplierDto.cs`) — no reshaping needed, the
 * DTOs already camelCase onto exactly this client's `Supplier`/
 * `SaveSupplierRequest` shape.
 */
export class HttpSupplierClient implements SupplierClient {
  async getAll(): Promise<Supplier[]> {
    return httpClient.get<Supplier[]>('/api/suppliers');
  }

  async create(request: SaveSupplierRequest): Promise<Supplier> {
    return httpClient.post<Supplier>('/api/suppliers', request);
  }

  async update(id: string, request: SaveSupplierRequest): Promise<Supplier> {
    return httpClient.put<Supplier>(`/api/suppliers/${id}`, request);
  }
}

/**
 * The client instance the app uses. `HttpSupplierClient` as of WP-027 —
 * swap this single line back to `new FixtureSupplierClient()` if the real
 * API becomes unreachable during local development.
 */
export const supplierClient: SupplierClient = new HttpSupplierClient();
