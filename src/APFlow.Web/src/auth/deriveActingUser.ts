import type { AccountInfo } from '@azure/msal-browser';
import type { ActingUser } from '@/auth/authContextDefinition';

/**
 * Maps an authenticated MSAL account to everything in `ActingUser` except
 * `roles` (WP-020 task 1; `roles` split out in WP-081 — see below).
 *
 * CLAIM SHAPES ARE UNCONFIRMED against a real issued token for the fields
 * below (as opposed to `roles`, which now is confirmed — see WP-081) —
 * same situation `docs/WP-002-Entra-Verification-Checklist.md` already
 * flags from the backend side. See
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
 *
 * `roles` is deliberately NOT derived here any more (WP-081): it used to
 * read the ID token's `roles` claim, but Entra only ever includes app-role
 * claims in a token issued for the resource (audience) the role is
 * assigned against — these roles are assigned against the API's service
 * principal (WP-064), not the SPA's, so the ID token (audience = SPA)
 * structurally never carries `roles` at all, confirmed by decoding a real,
 * live ID token during WP-025's QA pass. `AuthContext.tsx` now sources
 * `roles` from the access token instead (`decodeAccessTokenRoles.ts`), the
 * same token real server-side authorization already uses.
 */
export function deriveActingUser(account: AccountInfo): Omit<ActingUser, 'roles'> {
  const claims = (account.idTokenClaims ?? {}) as Record<string, unknown>;

  const displayName =
    account.name || (typeof claims.preferred_username === 'string' ? claims.preferred_username : account.username);

  return {
    tenantId: account.tenantId,
    tenantName: account.tenantId,
    displayName,
  };
}
