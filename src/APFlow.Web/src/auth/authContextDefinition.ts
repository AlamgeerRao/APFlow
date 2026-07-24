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
  /**
   * Application roles held by the acting user (see
   * `06_Domain_Reference_Data.md` §1 for the approved catalogue —
   * `PLATFORM_ADMIN`, `AP_REVIEWER`, `FINANCE_MANAGER`, `CREDIT_CONTROLLER`,
   * `ACCOUNTS_ADMIN`, `READ_ONLY`). Added for WP-018: role-gated workflow
   * actions (e.g. Approve) need to know which role(s) the acting user holds.
   * Real role assignment is owned by WP-002 (Entra) — this stand-in lets
   * WP-018 be verified against both a `FINANCE_MANAGER` and an
   * `AP_REVIEWER` acting user locally, per its own acceptance criteria.
   */
  roles: string[];
}

export interface AuthContextValue {
  user: ActingUser | null;
  isAuthenticated: boolean;
  signIn: (user: ActingUser) => void;
  signOut: () => void;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);
