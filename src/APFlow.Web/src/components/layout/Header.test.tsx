import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Header } from '@/components/layout/Header';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';

function renderHeader(roles: string[]) {
  const authValue: AuthContextValue = {
    user: { tenantId: 'gb-skips', tenantName: 'GB Skips', displayName: 'Test User', roles },
    isAuthenticated: true,
    signIn: () => {},
    signOut: () => {},
  };

  render(
    <AuthContext.Provider value={authValue}>
      <Header onToggleNav={vi.fn()} />
    </AuthContext.Provider>,
  );
}

describe('Header role label (WP-076)', () => {
  it('shows "Signed in as: Full Approver" for a FINANCE_MANAGER user', () => {
    renderHeader(['FINANCE_MANAGER']);

    expect(screen.getByText(/Signed in as: Full Approver/)).toBeInTheDocument();
  });

  it('shows "Signed in as: Standard Reviewer" for an AP_REVIEWER user', () => {
    renderHeader(['AP_REVIEWER']);

    expect(screen.getByText(/Signed in as: Standard Reviewer/)).toBeInTheDocument();
  });

  it('prioritises Full Approver when a user holds both roles', () => {
    renderHeader(['AP_REVIEWER', 'FINANCE_MANAGER']);

    expect(screen.getByText(/Signed in as: Full Approver/)).toBeInTheDocument();
  });

  it('falls back to the platform catalogue name for a non-GB-Skips role', () => {
    renderHeader(['READ_ONLY']);

    expect(screen.getByText(/Signed in as: Read-Only/)).toBeInTheDocument();
  });

  it('shows no role label for a user with no roles', () => {
    renderHeader([]);

    expect(screen.queryByText(/Signed in as:/)).not.toBeInTheDocument();
  });
});
