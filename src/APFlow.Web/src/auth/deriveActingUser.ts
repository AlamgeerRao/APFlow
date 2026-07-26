import type { AccountInfo } from '@azure/msal-browser';
import type { ActingUser } from '@/auth/authContextDefinition';

/**
 * Maps an authenticated MSAL account to our `ActingUser` shape (WP-020
 * task 1).
 *
 * CLAIM SHAPES ARE UNCONFIRMED against a real issued token — same
 * situation `docs/WP-002-Entra-Verification-Checklist.md` already flags
 * from the backend side ("role claim values, oid/tid/roles/
 * preferred_username claim shapes... are all best-effort assumptions,
 * never validated against a real issued token"). This function hits the
 * identical gap from the frontend. See
 * docs/WP-020-Real-Auth-And-Api-Integration-Decisions.md §1 for exactly
 * which claims are assumed and what to verify once a real token is
 * available.
 *
 * - `tenantId`: MSAL's own parsed `account.tenantId` (derived from the
 *   token's `tid` claim) — this is AP Flow's own multi-tenant key,
 *   matching the backend's `WorkflowTemplate.TenantId` (see
 *   `docs/WP-050-Workflow-Engine-Decisions.md`, "GB Skips' tenant id is a
 *   placeholder").
 * - `displayName`: `account.name`, falling back to the `preferred_username`
 *   claim, then the raw account username.
 * - `tenantName`: no standard Entra ID token claim carries a friendly
 *   organisation display name. Falls back to the tenant ID itself — a
 *   real friendly name would need either a custom claim configured on the
 *   app registration, or a separate "current tenant" API lookup; neither
 *   is implemented here.
 * - `roles`: the standard Entra App Roles claim (`roles`), per
 *   `06_Domain_Reference_Data.md` §1's catalogue — assumes the backend's
 *   Entra app registration is configured to issue app roles matching that
 *   catalogue exactly (PLATFORM_ADMIN/AP_REVIEWER/FINANCE_MANAGER/
 *   CREDIT_CONTROLLER/ACCOUNTS_ADMIN/READ_ONLY).
 */
export function deriveActingUser(account: AccountInfo): ActingUser {
  const claims = (account.idTokenClaims ?? {}) as Record<string, unknown>;

  const displayName =
    account.name || (typeof claims.preferred_username === 'string' ? claims.preferred_username : account.username);

  const roles = Array.isArray(claims.roles) ? claims.roles.filter((role): role is string => typeof role === 'string') : [];

  return {
    tenantId: account.tenantId,
    tenantName: account.tenantId,
    displayName,
    roles,
  };
}
