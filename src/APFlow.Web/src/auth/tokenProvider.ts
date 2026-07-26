import { InteractionRequiredAuthError } from '@azure/msal-browser';
import { getMsalInstance } from '@/auth/msalInstance';
import { apiTokenRequest } from '@/auth/msalConfig';

/**
 * Acquires a real access token for API calls (WP-020 task 2), used by
 * httpClient.ts. Decoupled from httpClient itself so the client stays
 * testable with plain fetch mocks, with no MSAL/React dependency of its
 * own.
 *
 * Returns null when there is no signed-in account at all (caller should
 * treat this as "not authenticated" and redirect to sign-in) — distinct
 * from a token that has genuinely expired mid-session, which triggers
 * `forceSignIn` directly here rather than surfacing a bare 401 to the
 * caller (task 3: session-expiry handling).
 */
export async function getAccessToken(): Promise<string | null> {
  const instance = getMsalInstance();
  const account = instance.getActiveAccount() ?? instance.getAllAccounts()[0];
  if (!account) return null;

  try {
    const result = await instance.acquireTokenSilent({ ...apiTokenRequest(), account });
    return result.accessToken;
  } catch (error) {
    if (error instanceof InteractionRequiredAuthError) {
      await forceSignIn();
      return null;
    }
    throw error;
  }
}

/** Forces a fresh interactive sign-in, e.g. after a 401 or a silent-token failure that needs user interaction. */
export async function forceSignIn(): Promise<void> {
  const instance = getMsalInstance();
  await instance.loginRedirect(apiTokenRequest());
}
