import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { InvoiceQueueTable } from '@/components/invoiceQueue/InvoiceQueueTable';
import type { InvoiceListItem } from '@/types/invoice';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';

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
  duplicateCheckReason: 'All fields matched an existing invoice from the same supplier.',
};

// InvoiceStatusBadge internally calls useWorkflowTemplate, which requires an
// authenticated acting user in context to resolve a tenant.
const authValue: AuthContextValue = {
  user: { tenantId: 'platform-default', tenantName: 'Platform Default Tenant', displayName: 'Test User' },
  isAuthenticated: true,
  signIn: () => {},
  signOut: () => {},
};

function renderTable(invoices: InvoiceListItem[], onSortChange = vi.fn()) {
  return render(
    <AuthContext.Provider value={authValue}>
      <InvoiceQueueTable invoices={invoices} sortBy="invoiceDate" sortDirection="asc" onSortChange={onSortChange} />
    </AuthContext.Provider>,
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
});
