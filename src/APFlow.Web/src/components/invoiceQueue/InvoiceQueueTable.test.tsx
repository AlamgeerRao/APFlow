import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { InvoiceQueueTable } from '@/components/invoiceQueue/InvoiceQueueTable';
import type { InvoiceListItem } from '@/types/invoice';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import { httpClient } from '@/api/httpClient';

// WP-075: InvoiceStatusBadge internally calls useWorkflowTemplate, which is now
// HTTP-backed (GET /api/workflow-template) rather than fixture-backed - mock
// httpClient.get directly so the real "Awaiting Review" label resolves.
vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn() } };
});

const nonDuplicateInvoice: InvoiceListItem = {
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
};

const duplicateInvoice: InvoiceListItem = {
  id: 'inv-2',
  supplierName: 'Contoso Supplies',
  invoiceNumber: 'CS-2045',
  invoiceDate: '2026-07-03',
  amount: 875,
  currencyCode: 'GBP',
  status: 'AWAITING_REVIEW',
  isPotentialDuplicate: true,
  duplicateCheckReason: "Matches an existing invoice on Supplier and Invoice Number ('CS-2046').",
  duplicateMatchInvoiceId: 'inv-7',
};

// WP-072: mirrors the real live invoice (Veygo / 2W4WVCTZ-0001) that crashed the
// entire table - Document Intelligence extracted no invoiceDate for it.
const missingDateInvoice: InvoiceListItem = {
  id: 'inv-3',
  supplierName: 'Veygo',
  invoiceNumber: '2W4WVCTZ-0001',
  invoiceDate: null,
  amount: 83.89,
  currencyCode: 'GBP',
  status: 'AWAITING_REVIEW',
  isPotentialDuplicate: true,
  duplicateCheckReason: "Matches an existing invoice on Supplier and Invoice Number ('2W4WVCTZ-0001').",
  duplicateMatchInvoiceId: 'inv-99',
};

// WP-077: mirrors the real live invoice (DigitalOcean) with a null
// supplierInvoiceNumber - Document Intelligence extracted no invoice number
// for it, same class of gap as missingDateInvoice's invoiceDate above.
const missingInvoiceNumberInvoice: InvoiceListItem = {
  id: 'inv-4',
  supplierName: 'DigitalOcean',
  invoiceNumber: null,
  invoiceDate: '2026-08-01',
  amount: null,
  currencyCode: null,
  status: 'AWAITING_REVIEW',
  isPotentialDuplicate: false,
  duplicateCheckReason: null,
  duplicateMatchInvoiceId: null,
};

// WP-077: supplierName is also genuinely nullable (invoice.Supplier?.Name),
// even though it's structurally rare in practice.
const missingSupplierNameInvoice: InvoiceListItem = {
  id: 'inv-5',
  supplierName: null,
  invoiceNumber: 'UNK-1',
  invoiceDate: '2026-08-01',
  amount: 50,
  currencyCode: 'GBP',
  status: 'AWAITING_REVIEW',
  isPotentialDuplicate: false,
  duplicateCheckReason: null,
  duplicateMatchInvoiceId: null,
};

// InvoiceStatusBadge internally calls useWorkflowTemplate, which requires an
// authenticated acting user in context to resolve a tenant.
const authValue: AuthContextValue = {
  user: { tenantId: 'platform-default', tenantName: 'Platform Default Tenant', displayName: 'Test User', roles: ['AP_REVIEWER'] },
  isAuthenticated: true,
  signIn: () => {},
  signOut: () => {},
};

beforeEach(() => {
  vi.mocked(httpClient.get).mockReset();
  vi.mocked(httpClient.get).mockResolvedValue({
    id: 'template-1',
    domainName: 'Invoice',
    name: 'Platform Default',
    isTenantSpecific: false,
    statuses: [
      { code: 'AWAITING_REVIEW', name: 'Awaiting Review', isTerminal: false, sortOrder: 4 },
    ],
    transitions: [],
  });
});

function renderTable(invoices: InvoiceListItem[], onSortChange = vi.fn()) {
  return render(
    <MemoryRouter>
      <AuthContext.Provider value={authValue}>
        <InvoiceQueueTable invoices={invoices} sortBy="invoiceDate" sortDirection="asc" onSortChange={onSortChange} />
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

/** Waits for InvoiceStatusBadge's async WorkflowTemplate fetch to resolve. */
async function waitForStatusBadgesToSettle() {
  await waitFor(() => {
    expect(screen.getAllByText('Awaiting Review').length).toBeGreaterThan(0);
  });
}

describe('InvoiceQueueTable', () => {
  it('renders the required columns for each invoice', async () => {
    renderTable([nonDuplicateInvoice]);
    await waitForStatusBadgesToSettle();

    expect(screen.getByText('Northwind Traders Ltd')).toBeInTheDocument();
    expect(screen.getByText('NW-1001')).toBeInTheDocument();
    expect(screen.getByText('01 Jul 2026')).toBeInTheDocument();
    expect(screen.getByText('£1,240.50')).toBeInTheDocument();
  });

  it('highlights a row flagged as a potential duplicate, and not a normal row', async () => {
    renderTable([nonDuplicateInvoice, duplicateInvoice]);
    await waitForStatusBadgesToSettle();

    const rows = screen.getAllByTestId('invoice-row');
    const normalRow = rows.find((row) => row.textContent?.includes('Northwind'));
    const duplicateRow = rows.find((row) => row.textContent?.includes('Contoso'));

    expect(duplicateRow?.getAttribute('data-duplicate')).toBe('true');
    expect(normalRow?.getAttribute('data-duplicate')).toBe('false');
    expect(screen.getByText('Possible duplicate')).toBeInTheDocument();
  });

  it('does not show a duplicate indicator for a non-duplicate row', async () => {
    renderTable([nonDuplicateInvoice]);
    await waitForStatusBadgesToSettle();

    expect(screen.queryByText('Possible duplicate')).not.toBeInTheDocument();
  });

  it('calls onSortChange with the column field when a sortable header is clicked', async () => {
    const onSortChange = vi.fn();
    const user = userEvent.setup();
    renderTable([nonDuplicateInvoice], onSortChange);
    await waitForStatusBadgesToSettle();

    await user.click(screen.getByRole('button', { name: /Sort by Amount/i }));

    expect(onSortChange).toHaveBeenCalledWith('amount');
  });

  it('shows an empty state when there are no invoices', () => {
    renderTable([]);

    expect(screen.getByText(/No invoices match/i)).toBeInTheDocument();
  });

  it('exposes each row as an accessible button-role element labelled with supplier and invoice number', async () => {
    renderTable([nonDuplicateInvoice]);
    await waitForStatusBadgesToSettle();

    expect(
      screen.getByRole('button', { name: /Review invoice NW-1001 from Northwind Traders Ltd/i }),
    ).toBeInTheDocument();
  });

  it('renders a row with a missing invoiceDate as a fallback instead of crashing the table', async () => {
    renderTable([nonDuplicateInvoice, missingDateInvoice]);
    await waitForStatusBadgesToSettle();

    // The whole table rendered - including the good row - proving one bad row
    // doesn't take down the page.
    expect(screen.getByText('Northwind Traders Ltd')).toBeInTheDocument();
    expect(screen.getByText('Veygo')).toBeInTheDocument();
    expect(screen.getByText('2W4WVCTZ-0001')).toBeInTheDocument();
    expect(screen.getByText('—')).toBeInTheDocument();
  });

  it('renders a row with a missing invoiceNumber as a fallback instead of crashing the table', async () => {
    renderTable([nonDuplicateInvoice, missingInvoiceNumberInvoice]);
    await waitForStatusBadgesToSettle();

    expect(screen.getByText('Northwind Traders Ltd')).toBeInTheDocument();
    expect(screen.getByText('DigitalOcean')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /Review invoice — from DigitalOcean/i }),
    ).toBeInTheDocument();
  });

  it('renders a row with a missing supplierName as a fallback instead of crashing the table', async () => {
    renderTable([nonDuplicateInvoice, missingSupplierNameInvoice]);
    await waitForStatusBadgesToSettle();

    expect(screen.getByText('UNK-1')).toBeInTheDocument();
    expect(
      screen.getByRole('button', { name: /Review invoice UNK-1 from —/i }),
    ).toBeInTheDocument();
  });
});
