import type { Configuration } from '@azure/msal-browser';

/**
 * MSAL configuration for real Entra External ID sign-in (WP-020 task 1),
 * replacing WP-014's `AuthContext.tsx` demo-tenant/role picker stub.
 *
 * Validation is intentionally lazy (only when `getMsalConfig`/`apiTokenRequest`
 * are actually called, not at module import time) so files that transitively
 * import auth types — including this project's existing test suite, which
 * provides `AuthContext` values directly and never renders the real
 * `AuthProvider` — don't fail just because `.env.local` isn't present. See
 * docs/WP-020-Real-Auth-And-Api-Integration-Decisions.md §1 for the full
 * list of environment variables and why each is required at real-auth
 * runtime but not at test time.
 */
function requireEnvVar(key: keyof ImportMetaEnv): string {
  const value = import.meta.env[key];
  if (!value) {
    throw new Error(
      `Missing required environment variable: ${key}. Copy .env.example to .env.local and fill in real values for this environment.`,
    );
  }
  return value;
}

export function getMsalConfig(): Configuration {
  return {
    auth: {
      clientId: requireEnvVar('VITE_ENTRA_CLIENT_ID'),
      authority: requireEnvVar('VITE_ENTRA_AUTHORITY'),
      redirectUri: import.meta.env.VITE_ENTRA_REDIRECT_URI || window.location.origin,
      postLogoutRedirectUri: import.meta.env.VITE_ENTRA_REDIRECT_URI || window.location.origin,
    },
    cache: {
      // sessionStorage (not localStorage): tokens don't persist across
      // browser restarts, reducing the window a stolen device exposes a
      // live session for — a reasonable default for a finance-adjacent
      // app, flagged for confirmation in the decisions doc.
      cacheLocation: 'sessionStorage',
    },
  };
}

/** The scope requested for both sign-in and API access tokens. */
export function apiTokenRequest(): { scopes: string[] } {
  return { scopes: [requireEnvVar('VITE_API_SCOPE')] };
}
