import { useMemo, type ReactNode } from 'react';
import { useMsal, useIsAuthenticated } from '@azure/msal-react';
import { AuthContext, type ActingUser, type AuthContextValue } from '@/auth/authContextDefinition';
import { apiTokenRequest } from '@/auth/msalConfig';
import { deriveActingUser } from '@/auth/deriveActingUser';

export type { ActingUser };

/**
 * Real Entra External ID-backed auth provider (WP-020 task 1), replacing
 * WP-014's demo-tenant/role picker stub entirely. Requires an ancestor
 * `MsalProvider` (wired in App.tsx) — `useMsal`/`useIsAuthenticated` throw
 * without one.
 *
 * Deliberately preserves the exact `AuthContextValue`/`ActingUser` shape
 * WP-014 established (`user`, `isAuthenticated`, `signIn`, `signOut`), so
 * every consumer built against it since (WP-015 through WP-019 — nav,
 * queue, review screen, notes, workflow actions, supplier views) needed
 * no changes at all. The one interface change: `signIn` no longer takes
 * an `ActingUser` parameter — real sign-in doesn't let the caller choose
 * who they are. `LoginPage.tsx` was updated for this; every other
 * consumer only ever called `signOut()`/read `user`, so nothing else
 * needed touching.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const { instance, accounts } = useMsal();
  const isAuthenticated = useIsAuthenticated();

  const account = accounts[0] ?? null;
  const user = useMemo<ActingUser | null>(() => (account ? deriveActingUser(account) : null), [account]);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: isAuthenticated && user !== null,
      signIn: () => {
        void instance.loginRedirect(apiTokenRequest());
      },
      signOut: () => {
        void instance.logoutRedirect();
      },
    }),
    [user, isAuthenticated, instance],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
