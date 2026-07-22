import { createContext } from 'react';

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
export interface ActingUser {
  tenantId: string;
  tenantName: string;
  displayName: string;
}

export interface AuthContextValue {
  user: ActingUser | null;
  isAuthenticated: boolean;
  signIn: (user: ActingUser) => void;
  signOut: () => void;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);
