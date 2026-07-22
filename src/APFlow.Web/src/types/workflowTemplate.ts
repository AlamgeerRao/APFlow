/**
 * Client-side shape for the tenant's Invoice workflow configuration
 * (WorkflowTemplate / StatusReference, per SA-007 E-10 / E-14 and
 * 06_Domain_Reference_Data.md §2).
 *
 * PROVISIONAL CONTRACT: WP-050 (Tenant-Configurable Workflow Engine) has not
 * been implemented yet. This shape is a reasoned proposal for what WP-014
 * needs to render the nav, documented in
 * docs/WP-014-Dashboard-Shell-Decisions.md and flagged there for Chief
 * Technical Architect / WP-050 sign-off. It is intentionally minimal and
 * additive so the real backend contract can extend it without breaking
 * this consumer.
 */

/** A single status in a tenant's Invoice workflow. */
export interface StatusReference {
  /** Stable status code, e.g. "AWAITING_REVIEW". Matches 06_Domain_Reference_Data.md §2. */
  code: string;
  /** Human-readable display name, e.g. "Awaiting Review". */
  name: string;
  /** Whether this status is a terminal state in the workflow (e.g. Archived). */
  isTerminal: boolean;
  /** Display/sequence order within the workflow, ascending. */
  order: number;
}

/** The acting tenant's full Invoice WorkflowTemplate. */
export interface WorkflowTemplate {
  tenantId: string;
  /** Display name of the template, e.g. "Platform Default" or "GB Skips". */
  templateName: string;
  statuses: StatusReference[];
}
