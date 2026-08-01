/**
 * Display names for the approved, platform-wide role catalogue
 * (`06_Domain_Reference_Data.md` §1). Reproduced verbatim from that
 * document — this file must never add, rename, or remove a role; it only
 * gives WP-018's user-facing rejection messages (task 7) a friendly name
 * instead of a raw role code.
 */
const ROLE_DISPLAY_NAMES: Record<string, string> = {
  PLATFORM_ADMIN: 'Platform Administrator',
  AP_REVIEWER: 'AP Reviewer',
  FINANCE_MANAGER: 'Finance Manager / Decision-Maker',
  CREDIT_CONTROLLER: 'Credit Controller',
  ACCOUNTS_ADMIN: 'Accounts Administrator',
  READ_ONLY: 'Read-Only',
};

/** Returns the approved catalogue's display name for a role code, falling back to the raw code for any value outside that catalogue. */
export function getRoleDisplayName(roleCode: string): string {
  return ROLE_DISPLAY_NAMES[roleCode] ?? roleCode;
}

/**
 * Returns the header's "Signed in as: …" role label (WP-076 task 6), sourced
 * from the same `roles` claim already used for action gating
 * (`RoleGatedTransitions`/`WorkflowActionsPanel`) - no new lookup.
 *
 * GB Skips' two-tier approval model - "Full/Approver" (`FINANCE_MANAGER`)
 * and "Standard/Reviewer" (`AP_REVIEWER`) - is the interim, approved mapping
 * onto the platform-wide role catalogue (`06_Domain_Reference_Data.md` §1),
 * and `RoleGatedTransitions` already treats `FINANCE_MANAGER` as the one
 * role every approval-type gate in this codebase checks, for every tenant,
 * not just GB Skips - so this labeling applies uniformly rather than
 * branching on tenant. A user holding neither of those two roles (e.g.
 * `PLATFORM_ADMIN`, `CREDIT_CONTROLLER`, `ACCOUNTS_ADMIN`, `READ_ONLY`) gets
 * the platform's own catalogue name via `getRoleDisplayName` instead - the
 * "platform-default equivalent" the task's own wording anticipates. `null`
 * only when the user holds no roles at all (nothing to label).
 * `FINANCE_MANAGER` takes priority over `AP_REVIEWER` when a user holds
 * both, matching `RoleGatedTransitions`' own "approval role wins" gating.
 */
export function getActingRoleLabel(roles: string[]): string | null {
  if (roles.includes('FINANCE_MANAGER')) return 'Full Approver';
  if (roles.includes('AP_REVIEWER')) return 'Standard Reviewer';
  if (roles.length > 0) return getRoleDisplayName(roles[0]);
  return null;
}
