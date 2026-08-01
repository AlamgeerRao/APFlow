import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { InvoiceQueueFilters } from '@/components/invoiceQueue/InvoiceQueueFilters';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';

// InvoiceQueueFilters internally calls useWorkflowTemplate, which requires an
// authenticated acting user in context to resolve a tenant (same as
// InvoiceQueueTable.test.tsx's InvoiceStatusBadge).
const authValue: AuthContextValue = {
  user: { tenantId: 'platform-default', tenantName: 'Platform Default Tenant', displayName: 'Test User', roles: ['AP_REVIEWER'] },
  isAuthenticated: true,
  signIn: () => {},
  signOut: () => {},
};

function renderFilters(hideStatusFilter?: boolean) {
  return render(
    <AuthContext.Provider value={authValue}>
      <InvoiceQueueFilters
        search=""
        onSearchChange={vi.fn()}
        status={undefined}
        onStatusChange={vi.fn()}
        hideStatusFilter={hideStatusFilter}
      />
    </AuthContext.Provider>,
  );
}

describe('InvoiceQueueFilters', () => {
  it('renders both the search box and the status dropdown by default', async () => {
    renderFilters();

    expect(screen.getByPlaceholderText(/Search by supplier or invoice number/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/Filter by status/i)).toBeInTheDocument();
    // Lets useWorkflowTemplate's async fetch settle before the test ends.
    await waitFor(() => expect(screen.getByLabelText(/Filter by status/i)).toBeInTheDocument());
  });

  // WP-074: the Query Queue view is already scoped to a fixed combined set of
  // statuses - a dropdown offering every other status wouldn't make sense there.
  it('hides the status dropdown, keeping the search box, when hideStatusFilter is true', async () => {
    renderFilters(true);

    expect(screen.getByPlaceholderText(/Search by supplier or invoice number/i)).toBeInTheDocument();
    expect(screen.queryByLabelText(/Filter by status/i)).not.toBeInTheDocument();
    // Lets useWorkflowTemplate's async fetch settle before the test ends.
    await waitFor(() => expect(screen.getByPlaceholderText(/Search by supplier or invoice number/i)).toBeInTheDocument());
  });
});
