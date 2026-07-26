import { createContext } from 'react';

/**
 * Acting-user/tenant shape consumed throughout the app (nav, queue,
 * review screen, notes, workflow actions, supplier views).
 *
 * Backed by real Microsoft Entra External ID sign-in as of WP-020 (see
 * `AuthContext.tsx`) — real role assignment and tenant identity come from
 * the signed-in user's Entra account. See `deriveActingUser.ts` for the
 * exact claim-to-field mapping and its unconfirmed-against-a-real-token
 * caveats.
 */
export interface ActingUser {
  tenantId: string;
  tenantName: string;
  displayName: string;
  /**
   * Application roles held by the acting user (see
   * `06_Domain_Reference_Data.md` §1 for the approved catalogue —
   * `PLATFORM_ADMIN`, `AP_REVIEWER`, `FINANCE_MANAGER`, `CREDIT_CONTROLLER`,
   * `ACCOUNTS_ADMIN`, `READ_ONLY`). Sourced from Entra's `roles` app-roles
   * claim as of WP-020.
   */
  roles: string[];
}

export interface AuthContextValue {
  user: ActingUser | null;
  isAuthenticated: boolean;
  /** Triggers Entra sign-in. Takes no parameters — unlike WP-014's demo stub, real sign-in doesn't let the caller choose who they are. */
  signIn: () => void;
  signOut: () => void;
}

export const AuthContext = createContext<AuthContextValue | undefined>(undefined);
