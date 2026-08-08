import { describe, expect, it, vi, beforeEach } from 'vitest';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { DashboardPage } from '@/pages/DashboardPage';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import { httpClient } from '@/api/httpClient';

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

const workflowTemplateResponse = {
  id: 'template-1',
  domainName: 'Invoice',
  name: 'Platform Default',
  isTenantSpecific: false,
  statuses: [{ code: 'AWAITING_REVIEW', name: 'Awaiting Review', isTerminal: false, sortOrder: 4 }],
  transitions: [],
};

function renderPage() {
  return render(
    <MemoryRouter>
      <AuthContext.Provider value={authValue}>
        <DashboardPage />
      </AuthContext.Provider>
    </MemoryRouter>,
  );
}

beforeEach(() => {
  vi.mocked(httpClient.get).mockReset();
  vi.mocked(httpClient.get).mockImplementation(async (path: string) => {
    if (path === '/api/workflow-template') return workflowTemplateResponse;
    if (path === '/api/invoices/folders') {
      return [{ statusCode: 'AWAITING_REVIEW', statusLabel: 'Awaiting Review', count: 4 }];
    }
    if (path === '/api/invoices') {
      return {
        items: [
          {
            id: 'inv-1',
            supplierName: 'Northwind Traders Ltd',
            supplierInvoiceNumber: 'NW-1001',
            status: 'AWAITING_REVIEW',
            createdAtUtc: '2026-08-01T09:15:00Z',
          },
        ],
      };
    }
    throw new Error(`Unexpected path in test: ${path}`);
  });
});

describe('DashboardPage', () => {
  it('shows a loading state, then the status counts grid and recent activity', async () => {
    renderPage();

    expect(screen.getByRole('status')).toBeInTheDocument();

    await waitFor(() => expect(screen.getByTestId('status-counts-grid')).toBeInTheDocument());

    expect(screen.getByText('4')).toBeInTheDocument();
    expect(screen.getAllByText('Awaiting Review').length).toBeGreaterThan(0);
    expect(screen.getByText('Northwind Traders Ltd')).toBeInTheDocument();
    expect(screen.getByText('NW-1001')).toBeInTheDocument();
  });

  it('shows an error state with a working retry on failure', async () => {
    vi.mocked(httpClient.get).mockImplementation(async (path: string) => {
      if (path === '/api/invoices/folders') throw new Error('network error');
      if (path === '/api/workflow-template') return workflowTemplateResponse;
      return { items: [] };
    });

    renderPage();

    await waitFor(() => expect(screen.getByRole('alert')).toBeInTheDocument());
    expect(screen.getByText(/Unable to load dashboard data/)).toBeInTheDocument();

    vi.mocked(httpClient.get).mockImplementation(async (path: string) => {
      if (path === '/api/workflow-template') return workflowTemplateResponse;
      if (path === '/api/invoices/folders') {
        return [{ statusCode: 'AWAITING_REVIEW', statusLabel: 'Awaiting Review', count: 1 }];
      }
      return { items: [] };
    });

    fireEvent.click(screen.getByRole('button', { name: 'Retry' }));

    await waitFor(() => expect(screen.getByTestId('status-counts-grid')).toBeInTheDocument());
  });
});
