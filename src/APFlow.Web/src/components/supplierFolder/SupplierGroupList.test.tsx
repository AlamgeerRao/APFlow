import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { SupplierGroupList } from '@/components/supplierFolder/SupplierGroupList';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import type { SupplierGroup } from '@/types/supplierFolder';
import type { Supplier } from '@/types/supplier';

const authValue: AuthContextValue = {
  user: { tenantId: 'platform-default', tenantName: 'Platform Default Tenant', displayName: 'Test User', roles: ['AP_REVIEWER'] },
  isAuthenticated: true,
  signIn: () => {},
  signOut: () => {},
};

const groups: SupplierGroup[] = [
  {
    supplierName: 'Northwind Traders Ltd',
    count: 1,
    invoices: [
      {
        id: 'inv-1',
        supplierName: 'Northwind Traders Ltd',
        invoiceNumber: 'NW-1001',
        invoiceDate: '2026-07-01',
        amount: 1240.5,
        currencyCode: 'GBP',
        status: 'AWAITING_REVIEW',
        isPotentialDuplicate: false,
        duplicateCheckReason: null,
        duplicateMatchInvoiceId: null,
      },
    ],
  },
];

function renderList(
  g: SupplierGroup[],
  props: { suppliersByName?: Map<string, Supplier>; onEdit?: (supplier: Supplier) => void } = {},
) {
  return render(
    <MemoryRouter>
      <AuthContext.Provider value={authValue}>
        <SupplierGroupList groups={g} {...props} />
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

const northwindSupplier: Supplier = {
  id: 'supplier-001',
  name: 'Northwind Traders Ltd',
  code: 'NORTH01',
  email: null,
  phone: null,
  creditLimit: 15000,
  paymentTermsDays: 30,
  accountingReference: null,
  status: 'ACTIVE',
  createdAtUtc: '2026-01-10T09:00:00Z',
};

describe('SupplierGroupList', () => {
  it('renders a heading with the supplier name and invoice count for each group', async () => {
    renderList(groups);

    expect(screen.getByRole('heading', { name: 'Northwind Traders Ltd' })).toBeInTheDocument();
    expect(screen.getByText('1 invoice')).toBeInTheDocument();
    await waitFor(() => expect(screen.getByText('NW-1001')).toBeInTheDocument());
  });

  it('uses plural "invoices" when a group has more than one', async () => {
    const twoInvoiceGroup: SupplierGroup[] = [{ ...groups[0], count: 2, invoices: [groups[0].invoices[0], { ...groups[0].invoices[0], id: 'inv-2', invoiceNumber: 'NW-1002' }] }];
    renderList(twoInvoiceGroup);

    await waitFor(() => expect(screen.getByText('2 invoices')).toBeInTheDocument());
  });

  it('shows an empty state when there are no groups', () => {
    renderList([]);

    expect(screen.getByText(/No suppliers match/i)).toBeInTheDocument();
  });

  // WP-027: Edit action, resolved from the real Supplier record via suppliersByName.
  it('shows an Edit action for a group whose name resolves to a real Supplier record', () => {
    const suppliersByName = new Map([['northwind traders ltd', northwindSupplier]]);
    renderList(groups, { suppliersByName, onEdit: vi.fn() });

    expect(screen.getByRole('button', { name: 'Edit' })).toBeInTheDocument();
  });

  it('resolves the group name to its Supplier case-insensitively and trimmed, matching ResolveSupplierAsync', () => {
    const suppliersByName = new Map([['northwind traders ltd', northwindSupplier]]);
    const mixedCaseGroups: SupplierGroup[] = [{ ...groups[0], supplierName: '  Northwind Traders Ltd  ' }];
    renderList(mixedCaseGroups, { suppliersByName, onEdit: vi.fn() });

    expect(screen.getByRole('button', { name: 'Edit' })).toBeInTheDocument();
  });

  it('omits the Edit action when no matching Supplier record is found', () => {
    renderList(groups, { suppliersByName: new Map(), onEdit: vi.fn() });

    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument();
  });

  it('omits the Edit action when onEdit is not supplied, even if a match exists', () => {
    const suppliersByName = new Map([['northwind traders ltd', northwindSupplier]]);
    renderList(groups, { suppliersByName });

    expect(screen.queryByRole('button', { name: 'Edit' })).not.toBeInTheDocument();
  });

  it('calls onEdit with the resolved Supplier record when Edit is clicked', async () => {
    const user = userEvent.setup();
    const onEdit = vi.fn();
    const suppliersByName = new Map([['northwind traders ltd', northwindSupplier]]);
    renderList(groups, { suppliersByName, onEdit });

    await user.click(screen.getByRole('button', { name: 'Edit' }));

    expect(onEdit).toHaveBeenCalledWith(northwindSupplier);
  });
});
