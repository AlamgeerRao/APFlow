import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { RecentActivityList } from '@/components/dashboard/RecentActivityList';
import type { RecentActivityItem } from '@/types/dashboard';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import { httpClient } from '@/api/httpClient';

// InvoiceStatusBadge internally calls useWorkflowTemplate (HTTP-backed) -
// same mocking pattern InvoiceQueueTable.test.tsx already establishes.
vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn() } };
});

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
    statuses: [{ code: 'AWAITING_REVIEW', name: 'Awaiting Review', isTerminal: false, sortOrder: 4 }],
    transitions: [],
  });
});

function renderList(items: RecentActivityItem[]) {
  return render(
    <MemoryRouter>
      <AuthContext.Provider value={authValue}>
        <RecentActivityList items={items} />
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

const item: RecentActivityItem = {
  id: 'inv-1',
  supplierName: 'Northwind Traders Ltd',
  invoiceNumber: 'NW-1001',
  status: 'AWAITING_REVIEW',
  createdAtUtc: '2026-08-01T09:15:00Z',
};

describe('RecentActivityList', () => {
  it('renders supplier, invoice number, status, and timestamp; links to the Review Screen', async () => {
    renderList([item]);
    await waitFor(() => expect(screen.getByText('Awaiting Review')).toBeInTheDocument());

    expect(screen.getByText('Northwind Traders Ltd')).toBeInTheDocument();
    expect(screen.getByText('NW-1001')).toBeInTheDocument();
    expect(screen.getByText('01 Aug 2026, 09:15')).toBeInTheDocument();

    const link = screen.getByRole('link');
    expect(link).toHaveAttribute('href', '/invoices/review/inv-1');
  });

  it('falls back to an em dash for a null supplier name or invoice number', async () => {
    renderList([{ ...item, supplierName: null, invoiceNumber: null }]);
    await waitFor(() => expect(screen.getByText('Awaiting Review')).toBeInTheDocument());

    expect(screen.getAllByText('—').length).toBe(2);
  });

  it('shows an empty state when there is no recent activity', () => {
    renderList([]);

    expect(screen.getByText(/No recent invoice activity/)).toBeInTheDocument();
  });
});
