import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter, Route, Routes, useLocation } from 'react-router-dom';
import { ProtectedRoute } from '@/auth/ProtectedRoute';
import { AuthContext, type AuthContextValue } from '@/auth/authContextDefinition';

function renderWithAuth(isAuthenticated: boolean, initialPath = '/invoices') {
  const authValue: AuthContextValue = {
    user: isAuthenticated
      ? { tenantId: 'platform-default', tenantName: 'Platform Default Tenant', displayName: 'Test User', roles: ['AP_REVIEWER'] }
      : null,
    isAuthenticated,
    signIn: () => {},
    signOut: () => {},
  };

  function LoginStandIn() {
    const location = useLocation();
    const from = (location.state as { from?: { pathname: string } } | null)?.from;
    return <div>Login page (return path: {from?.pathname ?? 'none'})</div>;
  }

  return render(
    <AuthContext.Provider value={authValue}>
      <MemoryRouter initialEntries={[initialPath]}>
        <Routes>
          <Route path="/login" element={<LoginStandIn />} />
          <Route
            path="*"
            element={
              <ProtectedRoute>
                <div>Protected content</div>
              </ProtectedRoute>
            }
          />
        </Routes>
      </MemoryRouter>
    </AuthContext.Provider>,
  );
}

describe('ProtectedRoute', () => {
  it('renders the protected children when the user is authenticated', () => {
    renderWithAuth(true);

    expect(screen.getByText('Protected content')).toBeInTheDocument();
  });

  it('redirects to /login when the user is not authenticated', () => {
    renderWithAuth(false);

    expect(screen.queryByText('Protected content')).not.toBeInTheDocument();
    expect(screen.getByText(/Login page/)).toBeInTheDocument();
  });

  it('preserves the originally requested location so sign-in can return the user there', () => {
    renderWithAuth(false, '/invoices/awaiting-review');

    expect(screen.getByText('Login page (return path: /invoices/awaiting-review)')).toBeInTheDocument();
  });
});
