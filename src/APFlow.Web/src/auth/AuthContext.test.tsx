import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import type { AccountInfo } from '@azure/msal-browser';
import { AuthProvider } from '@/auth/AuthContext';
import { Header } from '@/components/layout/Header';

/** Builds a syntactically-real (unsigned) JWT for a given payload. */
function fakeJwt(payload: unknown): string {
  const base64url = (value: unknown) =>
    btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${base64url({ alg: 'none', typ: 'JWT' })}.${base64url(payload)}.signature`;
}

// This account's ID token deliberately carries NO roles claim - the real,
// live shape confirmed by decoding an actual issued ID token during
// WP-025's QA pass, not a hypothetical. If Header's role label ever
// regresses to reading account.idTokenClaims.roles again (WP-020/076's
// original bug), this test fails because roles would resolve to [].
const account: AccountInfo = {
  homeAccountId: 'home-1',
  environment: 'login.microsoftonline.com',
  tenantId: 'tenant-abc',
  username: 'approver@example.com',
  localAccountId: 'local-1',
  name: 'APFlow Test Approver',
  idTokenClaims: {},
};

vi.mock('@azure/msal-react', () => ({
  useMsal: () => ({ instance: { logoutRedirect: vi.fn(), loginRedirect: vi.fn() }, accounts: [account] }),
  useIsAuthenticated: () => true,
}));

vi.mock('@/auth/tokenProvider', () => ({
  getAccessToken: vi.fn(),
}));

describe('Header role label sourced from the access token (WP-081)', () => {
  it('shows the correct role label using the access token roles claim, not the (roles-less) ID token', async () => {
    const { getAccessToken } = await import('@/auth/tokenProvider');
    vi.mocked(getAccessToken).mockResolvedValue(fakeJwt({ aud: 'api://backend', roles: ['FINANCE_MANAGER'] }));

    render(
      <AuthProvider>
        <Header onToggleNav={vi.fn()} />
      </AuthProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText(/Signed in as: Full Approver/)).toBeInTheDocument();
    });
  });

  it('shows Standard Reviewer for an AP_REVIEWER access token', async () => {
    const { getAccessToken } = await import('@/auth/tokenProvider');
    vi.mocked(getAccessToken).mockResolvedValue(fakeJwt({ aud: 'api://backend', roles: ['AP_REVIEWER'] }));

    render(
      <AuthProvider>
        <Header onToggleNav={vi.fn()} />
      </AuthProvider>,
    );

    await waitFor(() => {
      expect(screen.getByText(/Signed in as: Standard Reviewer/)).toBeInTheDocument();
    });
  });
});
