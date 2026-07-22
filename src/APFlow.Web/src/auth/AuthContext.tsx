import { useMemo, useState, type ReactNode } from 'react';
import { AuthContext, type ActingUser, type AuthContextValue } from '@/auth/authContextDefinition';

export type { ActingUser };

/**
 * Minimal acting-user/tenant shape needed by WP-014 (shell + nav).
 *
 * PROVISIONAL: real authentication is Microsoft Entra External ID
 * (01_Project_Context.md §6/§5). WP-002 (Authentication & RBAC) owns the
 * real sign-in flow; that is out of scope here — "No business
 * functionality" per the WP-014 brief. This context stands in for it so
 * ProtectedRoute and the nav have something real to consume, and is
 * documented as an open item in docs/WP-014-Dashboard-Shell-Decisions.md.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<ActingUser | null>(null);

  const value = useMemo<AuthContextValue>(
    () => ({
      user,
      isAuthenticated: user !== null,
      signIn: (nextUser: ActingUser) => setUser(nextUser),
      signOut: () => setUser(null),
    }),
    [user],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
