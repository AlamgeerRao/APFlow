/**
 * Fixture-backed workflow transition graph for WP-018.
 *
 * IMPORTANT — reflects real, mixed confirmation status, not a single
 * uniform fixture:
 *
 * - `CHECKED_READY_TO_APPROVE -> APPROVED`, `CHECKED_READY_TO_APPROVE ->
 *   NEEDS_QUERY`, `REJECTED -> AWAITING_REVIEW`, and `CANCELLED ->
 *   RECEIVED` (all four requiring `FINANCE_MANAGER`) are the four
 *   transitions actually seeded and enforced on the backend today
 *   (`WorkflowTransitionSeedData`/`RoleGatedTransitions`, gated by
 *   `InvoiceService.UpdateAsync` — WP-053). Corrected here (2026-07-25,
 *   Chief Technical Architect ruling on
 *   docs/WP-018-Invoice-Workflow-Actions-Decisions.md) from WP-018's
 *   original single-edge assumption (only `CHECKED_READY_TO_APPROVE ->
 *   APPROVED`, WP-051's narrower interim scope) — the other three were
 *   missing from this fixture entirely, not merely un-gated. The reopen
 *   edges (`REJECTED -> AWAITING_REVIEW`, `CANCELLED -> RECEIVED`) are
 *   present in BOTH templates' real graphs, so both appear under
 *   `platform-default` and `gb-skips` below.
 * - The other three GB Skips edges below (`AWAITING_REVIEW ->
 *   CHECKED_READY_TO_APPROVE`, the two `NEEDS_REVIEW_FEBINA` escalation
 *   edges, and the three `NEEDS_REVIEW_FEBINA` resolution edges) ARE now
 *   seeded and enforced by WP-053 too (57 confirmed rows total), but this
 *   fixture has not been reconciled against that full graph - only the
 *   four role-gated edges above were corrected, per the specific ruling
 *   that flagged them. A full reconciliation is the same fixture-to-real
 *   HTTP client swap already tracked in docs/Backlog.md, not attempted
 *   piecemeal here.
 * - The platform-default template otherwise still has no entries beyond
 *   the two reopen edges above - WP-053's confirmed platform-default graph
 *   (25 transitions) has not been reconciled into this fixture either, for
 *   the same reason.
 */

/** Internal, not exported from `types/` — real `WorkflowAction`s are the post-permission-filter result the client produces from these. */
interface RawWorkflowAction {
  toStatusCode: string;
  label: string;
  /** Role code required to perform this action, or null if any authenticated tenant user with workflow access may. */
  requiredRole: string | null;
}

/** `Record<tenantId, Record<fromStatusCode, RawWorkflowAction[]>>`. */
export const workflowActionsByTenantAndStatus: Record<string, Record<string, RawWorkflowAction[]>> = {
  'platform-default': {
    REJECTED: [{ toStatusCode: 'AWAITING_REVIEW', label: 'Reopen for Review', requiredRole: 'FINANCE_MANAGER' }],
    CANCELLED: [{ toStatusCode: 'RECEIVED', label: 'Reopen (Restart Processing)', requiredRole: 'FINANCE_MANAGER' }],
  },
  'gb-skips': {
    AWAITING_REVIEW: [
      { toStatusCode: 'CHECKED_READY_TO_APPROVE', label: 'Mark Checked & Ready to Approve', requiredRole: null },
      { toStatusCode: 'NEEDS_REVIEW_FEBINA', label: 'Escalate to Febina', requiredRole: null },
    ],
    CHECKED_READY_TO_APPROVE: [
      { toStatusCode: 'APPROVED', label: 'Approve', requiredRole: 'FINANCE_MANAGER' },
      { toStatusCode: 'NEEDS_QUERY', label: 'Send Query', requiredRole: 'FINANCE_MANAGER' },
      { toStatusCode: 'NEEDS_REVIEW_FEBINA', label: 'Escalate to Febina', requiredRole: null },
    ],
    NEEDS_REVIEW_FEBINA: [
      { toStatusCode: 'CHECKED_READY_TO_APPROVE', label: 'Resolve: Mark Ready to Approve', requiredRole: null },
      { toStatusCode: 'NEEDS_QUERY', label: 'Resolve: Send Query', requiredRole: null },
      { toStatusCode: 'REJECTED', label: 'Resolve: Reject Invoice', requiredRole: null },
    ],
    REJECTED: [{ toStatusCode: 'AWAITING_REVIEW', label: 'Reopen for Review', requiredRole: 'FINANCE_MANAGER' }],
    CANCELLED: [{ toStatusCode: 'RECEIVED', label: 'Reopen (Restart Processing)', requiredRole: 'FINANCE_MANAGER' }],
  },
};
