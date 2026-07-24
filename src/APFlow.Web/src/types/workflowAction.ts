/**
 * Client-side shape for a single available workflow action on an invoice
 * (WP-018 — Invoice Workflow Actions).
 *
 * CONTRACT NOTE: WP-054 (Invoice Workflow Transition API) is now in
 * progress and specifies `GET /api/invoices/{id}/available-actions` as
 * returning `{ targetStatusCode, targetStatusLabel }[]` — this type's
 * field names were updated to match exactly (previously `toStatusCode`/
 * `label`), so swapping in the real client requires no field renaming.
 * See docs/WP-018-Invoice-Workflow-Actions-Decisions.md §1 for the
 * original gap analysis and the full note on what's now confirmed vs.
 * still open (the PATCH response returning a full `InvoiceDetail` rather
 * than just the new status code, and the optional `notes` field this
 * client's UI doesn't yet collect).
 *
 * Deliberately pre-filtered/resolved, not a raw `WorkflowTransition` +
 * separate `ApprovalPolicy` pair: this mirrors what a real
 * "list actions available to ME, right now" endpoint would return (already
 * evaluated server-side against the caller's actual roles), per WP-018 task
 * 3's own instruction — "query the user's permission before rendering the
 * button, not just before submitting." A `WorkflowAction` appearing in a
 * list returned by `WorkflowActionClient.getAvailableActions` means the
 * caller is both a valid transition target for the invoice's current status
 * AND permitted for the acting user — there is no separate
 * "visible but disabled" state to represent, matching WP-018's own explicit
 * "do not render or enable" wording (not "render disabled").
 */
export interface WorkflowAction {
  /** The status code this action would transition the invoice to. */
  targetStatusCode: string;
  /** Human-readable button label, e.g. "Approve", "Escalate to Febina" — distinct from the destination status's own display name, since one destination can be reached via differently-worded actions from different starting statuses (see the Resolve-* actions from NEEDS_REVIEW_FEBINA). */
  targetStatusLabel: string;
}
