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
