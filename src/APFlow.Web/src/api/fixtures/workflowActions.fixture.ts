/**
 * Fixture-backed workflow transition graph for WP-018.
 *
 * IMPORTANT — reflects real, mixed confirmation status, not a single
 * uniform fixture:
 *
 * - `CHECKED_READY_TO_APPROVE -> APPROVED` (GB Skips, requiredRole
 *   `FINANCE_MANAGER`) is the ONE transition actually seeded and enforced
 *   on the backend today (`WorkflowTransitionConfiguration`, gated by
 *   `InvoiceService.UpdateAsync`'s narrow role check — WP-051).
 * - The other three GB Skips edges below (`AWAITING_REVIEW ->
 *   CHECKED_READY_TO_APPROVE`, the two `NEEDS_REVIEW_FEBINA` escalation
 *   edges, and the three `NEEDS_REVIEW_FEBINA` resolution edges) are NOT
 *   seeded or enforced anywhere on the backend today. They are reproduced
 *   here verbatim from `docs/WP-050-Workflow-Engine-Decisions.md`'s own
 *   "Task 4 — the proposed transition set" section — i.e. this is the
 *   backend team's own already-recorded proposal, not a new business rule
 *   invented for this delivery — but that document's own status line reads
 *   "OPEN... needs Chief Technical Architect sign-off," and WP-051 (which
 *   HAS shipped) explicitly seeded only the one edge above, leaving these
 *   three "unconfirmed and unseeded." See
 *   docs/WP-018-Invoice-Workflow-Actions-Decisions.md §1 for the full
 *   analysis and why this delivery proceeded against them anyway.
 * - The platform-default template has ZERO entries below, for the same
 *   reason WP-050 itself gave for not building against one: "the
 *   platform-default transition graph is not documented anywhere in this
 *   project's reference material" — proposing nothing here rather than
 *   inventing one (see decisions doc §2).
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
  'platform-default': {},
  'gb-skips': {
    AWAITING_REVIEW: [
      { toStatusCode: 'CHECKED_READY_TO_APPROVE', label: 'Mark Checked & Ready to Approve', requiredRole: null },
      { toStatusCode: 'NEEDS_REVIEW_FEBINA', label: 'Escalate to Febina', requiredRole: null },
    ],
    CHECKED_READY_TO_APPROVE: [
      { toStatusCode: 'APPROVED', label: 'Approve', requiredRole: 'FINANCE_MANAGER' },
      { toStatusCode: 'NEEDS_REVIEW_FEBINA', label: 'Escalate to Febina', requiredRole: null },
    ],
    NEEDS_REVIEW_FEBINA: [
      { toStatusCode: 'CHECKED_READY_TO_APPROVE', label: 'Resolve: Mark Ready to Approve', requiredRole: null },
      { toStatusCode: 'NEEDS_QUERY', label: 'Resolve: Send Query', requiredRole: null },
      { toStatusCode: 'REJECTED', label: 'Resolve: Reject Invoice', requiredRole: null },
    ],
  },
};
