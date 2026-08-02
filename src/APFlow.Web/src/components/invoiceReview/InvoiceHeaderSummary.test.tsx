import { describe, expect, it, vi, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { InvoiceHeaderSummary } from '@/components/invoiceReview/InvoiceHeaderSummary';
import type { InvoiceDetail } from '@/types/invoiceDetail';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';
import { httpClient } from '@/api/httpClient';

// Same reasoning as InvoiceQueueTable.test.tsx: InvoiceStatusBadge internally
// calls useWorkflowTemplate, which is HTTP-backed and needs an authenticated
// acting user in context to resolve a tenant.
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

const baseInvoice: Omit<InvoiceDetail, 'pdfUrl'> = {
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
  sourceDocumentBlobName: 'blob-1',
  receivedAt: '2026-07-01T08:00:00Z',
  extractedFields: [],
  overallConfidenceScore: 0.9,
  auditEntries: [],
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

function renderSummary(invoice: Omit<InvoiceDetail, 'pdfUrl'>) {
  return render(
    <AuthContext.Provider value={authValue}>
      <InvoiceHeaderSummary invoice={invoice as InvoiceDetail} />
    </AuthContext.Provider>,
  );
}

async function waitForStatusBadgeToSettle() {
  await waitFor(() => {
    expect(screen.getByText('Awaiting Review')).toBeInTheDocument();
  });
}

describe('InvoiceHeaderSummary', () => {
  it('renders supplier and invoice number when both are present', async () => {
    renderSummary(baseInvoice);
    await waitForStatusBadgeToSettle();

    expect(screen.getByText('Northwind Traders Ltd')).toBeInTheDocument();
    expect(screen.getByText('NW-1001')).toBeInTheDocument();
  });

  // WP-077: supplierName is genuinely nullable (invoice.Supplier?.Name) -
  // renders a fallback instead of a blank field.
  it('renders a fallback instead of a blank field for a null supplierName', async () => {
    renderSummary({ ...baseInvoice, supplierName: null });
    await waitForStatusBadgeToSettle();

    expect(screen.getByText('Supplier').nextSibling?.textContent).toBe('—');
  });

  // WP-077: invoiceNumber is genuinely nullable (backend `string?
  // SupplierInvoiceNumber`) - renders a fallback instead of a blank field.
  it('renders a fallback instead of a blank field for a null invoiceNumber', async () => {
    renderSummary({ ...baseInvoice, invoiceNumber: null });
    await waitForStatusBadgeToSettle();

    expect(screen.getByText('Invoice Number').nextSibling?.textContent).toBe('—');
  });
});
