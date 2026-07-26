import { describe, expect, it } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { SupplierGroupList } from '@/components/supplierFolder/SupplierGroupList';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import type { SupplierGroup } from '@/types/supplierFolder';

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
      },
    ],
  },
];

function renderList(g: SupplierGroup[]) {
  return render(
    <MemoryRouter>
      <AuthContext.Provider value={authValue}>
        <SupplierGroupList groups={g} />
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

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
});
