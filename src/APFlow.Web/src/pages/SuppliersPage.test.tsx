import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { SuppliersPage } from '@/pages/SuppliersPage';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import { httpClient } from '@/api/httpClient';

vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn(), post: vi.fn(), put: vi.fn() } };
});

const supplierDto = {
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

function mockEndpoints() {
  vi.mocked(httpClient.get).mockImplementation(async (path: string) => {
    if (path === '/api/suppliers') return [supplierDto];
    if (path === '/api/invoices/folders') return [];
    if (path === '/api/invoices/suppliers') return ['Northwind Traders Ltd'];
    if (path === '/api/invoices/grouped') {
      return {
        groups: [{ supplierName: 'Northwind Traders Ltd', count: 0, invoices: [] }],
        totalSuppliers: 1,
        page: 1,
        pageSize: 5,
      };
    }
    throw new Error(`Unexpected GET ${path}`);
  });
}

function renderPage(roles: string[]) {
  const authValue: AuthContextValue = {
    user: { tenantId: 'gb-skips', tenantName: 'GB Skips', displayName: 'Test User', roles },
    isAuthenticated: true,
    signIn: () => {},
    signOut: () => {},
  };
  return render(
    <MemoryRouter>
      <AuthContext.Provider value={authValue}>
        <SuppliersPage />
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.mocked(httpClient.get).mockReset();
  vi.mocked(httpClient.post).mockReset();
  vi.mocked(httpClient.put).mockReset();
  mockEndpoints();
});

describe('SuppliersPage — create/edit wiring (WP-027)', () => {
  it('opens the Add supplier form when "+ Add supplier" is clicked, and hides the button while it is open', async () => {
    const user = userEvent.setup();
    renderPage(['AP_REVIEWER']);

    await user.click(screen.getByRole('button', { name: '+ Add supplier' }));

    expect(screen.getByRole('heading', { name: 'Add supplier' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: '+ Add supplier' })).not.toBeInTheDocument();
  });

  it('closes the form on Cancel and shows the "+ Add supplier" button again', async () => {
    const user = userEvent.setup();
    renderPage(['AP_REVIEWER']);

    await user.click(screen.getByRole('button', { name: '+ Add supplier' }));
    await user.click(screen.getByRole('button', { name: 'Cancel' }));

    expect(screen.queryByRole('heading', { name: 'Add supplier' })).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: '+ Add supplier' })).toBeInTheDocument();
  });

  it('opens the Edit form for the group\'s resolved Supplier when its Edit action is clicked', async () => {
    const user = userEvent.setup();
    renderPage(['AP_REVIEWER']);

    await waitFor(() => expect(screen.getByRole('button', { name: 'Edit' })).toBeInTheDocument());
    await user.click(screen.getByRole('button', { name: 'Edit' }));

    expect(screen.getByRole('heading', { name: 'Edit Northwind Traders Ltd' })).toBeInTheDocument();
    expect(screen.getByLabelText('Name')).toHaveValue('Northwind Traders Ltd');
  });

  it('a FINANCE_MANAGER user sees an editable Credit Limit input when editing', async () => {
    const user = userEvent.setup();
    renderPage(['FINANCE_MANAGER']);

    await waitFor(() => expect(screen.getByRole('button', { name: 'Edit' })).toBeInTheDocument());
    await user.click(screen.getByRole('button', { name: 'Edit' }));

    expect(screen.getByLabelText('Credit limit')).toHaveValue(15000);
  });

  it('a non-FINANCE_MANAGER user sees read-only Credit Limit text (not hidden, not disabled) when editing', async () => {
    const user = userEvent.setup();
    renderPage(['AP_REVIEWER']);

    await waitFor(() => expect(screen.getByRole('button', { name: 'Edit' })).toBeInTheDocument());
    await user.click(screen.getByRole('button', { name: 'Edit' }));

    expect(screen.getByTestId('credit-limit-readonly')).toHaveTextContent('15,000.00');
    expect(screen.queryByLabelText('Credit limit')).not.toBeInTheDocument();
  });

  it('submitting the Add supplier form calls POST /api/suppliers and closes the form on success', async () => {
    const user = userEvent.setup();
    vi.mocked(httpClient.post).mockResolvedValueOnce({
      ...supplierDto,
      id: 'supplier-002',
      name: 'Brand New Supplier',
      creditLimit: null,
    });
    renderPage(['AP_REVIEWER']);

    await user.click(screen.getByRole('button', { name: '+ Add supplier' }));
    await user.type(screen.getByLabelText('Name'), 'Brand New Supplier');
    await user.click(screen.getByRole('button', { name: 'Add supplier' }));

    await waitFor(() => expect(httpClient.post).toHaveBeenCalledWith('/api/suppliers', expect.objectContaining({ name: 'Brand New Supplier' })));
    await waitFor(() => expect(screen.queryByRole('heading', { name: 'Add supplier' })).not.toBeInTheDocument());
  });
});

describe('SuppliersPage — suppliers with no invoices yet (WP-093)', () => {
  it('surfaces a supplier with zero invoices in its own section, separate from the invoice-grouped list', async () => {
    const brandNewSupplierDto = { ...supplierDto, id: 'supplier-002', name: 'Brand New Supplier', creditLimit: null };
    vi.mocked(httpClient.get).mockImplementation(async (path: string) => {
      if (path === '/api/suppliers') return [supplierDto, brandNewSupplierDto];
      if (path === '/api/invoices/folders') return [];
      // Only Northwind has an invoice — Brand New Supplier is absent here,
      // exactly the case this section exists to cover.
      if (path === '/api/invoices/suppliers') return ['Northwind Traders Ltd'];
      if (path === '/api/invoices/grouped') {
        return {
          groups: [{ supplierName: 'Northwind Traders Ltd', count: 0, invoices: [] }],
          totalSuppliers: 1,
          page: 1,
          pageSize: 5,
        };
      }
      throw new Error(`Unexpected GET ${path}`);
    });
    renderPage(['AP_REVIEWER']);

    expect(await screen.findByRole('heading', { name: 'Suppliers with no invoices yet' })).toBeInTheDocument();
    expect(screen.getByText('Brand New Supplier')).toBeInTheDocument();
    // Northwind has an invoice, so it belongs only to the grouped list below.
    const noInvoicesSection = screen.getByRole('heading', { name: 'Suppliers with no invoices yet' }).closest('div');
    expect(noInvoicesSection).not.toHaveTextContent('Northwind Traders Ltd');
  });

  it('shows a newly created supplier in this section immediately, without needing an invoice first', async () => {
    const user = userEvent.setup();
    const brandNewSupplierDto = { ...supplierDto, id: 'supplier-002', name: 'Brand New Supplier', creditLimit: null };
    let created = false;
    vi.mocked(httpClient.get).mockImplementation(async (path: string) => {
      if (path === '/api/suppliers') return created ? [supplierDto, brandNewSupplierDto] : [supplierDto];
      if (path === '/api/invoices/folders') return [];
      if (path === '/api/invoices/suppliers') return ['Northwind Traders Ltd'];
      if (path === '/api/invoices/grouped') {
        return {
          groups: [{ supplierName: 'Northwind Traders Ltd', count: 0, invoices: [] }],
          totalSuppliers: 1,
          page: 1,
          pageSize: 5,
        };
      }
      throw new Error(`Unexpected GET ${path}`);
    });
    vi.mocked(httpClient.post).mockImplementation(async () => {
      created = true;
      return brandNewSupplierDto;
    });
    renderPage(['AP_REVIEWER']);

    await user.click(screen.getByRole('button', { name: '+ Add supplier' }));
    await user.type(screen.getByLabelText('Name'), 'Brand New Supplier');
    await user.click(screen.getByRole('button', { name: 'Add supplier' }));

    expect(await screen.findByRole('heading', { name: 'Suppliers with no invoices yet' })).toBeInTheDocument();
    expect(screen.getByText('Brand New Supplier')).toBeInTheDocument();
  });
});
