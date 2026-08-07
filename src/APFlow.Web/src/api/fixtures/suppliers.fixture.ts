import type { Supplier } from '@/types/supplier';
import { SUPPLIER_STATUS_ACTIVE, SUPPLIER_STATUS_INACTIVE } from '@/types/supplier';

/**
 * Seed suppliers for `FixtureSupplierClient` (WP-027). Names deliberately
 * match `invoiceFixtures`' own supplier names (`Northwind Traders Ltd`,
 * `Contoso Supplies`) so a fixture-driven `SuppliersPage` can resolve a
 * `SupplierGroupList` group back to a full `Supplier` record the same way
 * the real API does — one supplier with a `CreditLimit` set (exercising the
 * Finance Manager-only editable path) and one without.
 */
export const supplierFixtures: Supplier[] = [
  {
    id: 'supplier-001',
    name: 'Northwind Traders Ltd',
    code: 'NORTH01',
    email: 'accounts@northwindtraders.example',
    phone: '+44 20 7946 0001',
    creditLimit: 15000,
    paymentTermsDays: 30,
    accountingReference: 'NORTHW',
    status: SUPPLIER_STATUS_ACTIVE,
    createdAtUtc: '2026-01-10T09:00:00Z',
  },
  {
    id: 'supplier-002',
    name: 'Contoso Supplies',
    code: null,
    email: null,
    phone: null,
    creditLimit: null,
    paymentTermsDays: null,
    accountingReference: null,
    status: SUPPLIER_STATUS_ACTIVE,
    createdAtUtc: '2026-02-05T09:00:00Z',
  },
  {
    id: 'supplier-003',
    name: 'Fabrikam Waste Services',
    code: 'FAB01',
    email: 'billing@fabrikam.example',
    phone: null,
    creditLimit: 5000,
    paymentTermsDays: 14,
    accountingReference: 'FABRK',
    status: SUPPLIER_STATUS_INACTIVE,
    createdAtUtc: '2026-03-01T09:00:00Z',
  },
];
