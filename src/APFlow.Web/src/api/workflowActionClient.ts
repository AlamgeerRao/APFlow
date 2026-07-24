import type { WorkflowAction } from '@/types/workflowAction';
import { workflowActionsByTenantAndStatus } from '@/api/fixtures/workflowActions.fixture';
import { updateFixtureInvoiceStatus } from '@/api/fixtures/invoices.fixture';
import { getRoleDisplayName } from '@/utils/roles';

/**
 * Client-side contract for the acting tenant's Invoice workflow actions
 * (WP-018). No real HTTP endpoint exists yet for any of this — see
 * docs/WP-018-Invoice-Workflow-Actions-Decisions.md §1 for the full gap
 * analysis (no invoice-update endpoint of any kind exists in
 * `InvoicesController` today, let alone one that lists/executes
 * tenant-configured workflow actions) and a proposed, non-binding HTTP
 * contract. All consumers (`useWorkflowActions`) depend only on this
 * interface — swapping in a real HTTP client is a one-line change
 * (`workflowActionClient` below).
 */
export interface WorkflowActionClient {
  /**
   * Returns the actions available to THIS acting user, for an invoice
   * currently in `fromStatusCode`, for the given tenant — already filtered
   * by permission (WP-018 task 3: "query the user's permission before
   * rendering the button, not just before submitting"). An action absent
   * from this list is not merely hidden — as far as this client's caller is
   * concerned, it does not exist as an option for this user right now.
   */
  getAvailableActions(tenantId: string, fromStatusCode: string, userRoles: string[]): Promise<WorkflowAction[]>;

  /**
   * Executes a transition, re-checking permission before mutating (defense
   * in depth — the same "do not trust only the caller's own earlier check"
   * reasoning `FixtureInvoiceNoteClient.addNote` uses, and the same
   * "checked and rejected before any field is mutated" guarantee
   * `InvoiceService.UpdateAsync`'s real WP-051 role gate makes). Rejects
   * with a specific, human-readable message when the rejection is due to
   * an insufficient role (WP-018 task 7's explicit requirement) — this is
   * the path exercised when a permission re-check fails despite the UI
   * having already filtered the button out (e.g. a stale render), not a
   * scenario expected to occur via normal use of an up-to-date UI.
   */
  executeAction(
    tenantId: string,
    invoiceId: string,
    fromStatusCode: string,
    targetStatusCode: string,
    userRoles: string[],
  ): Promise<{ newStatusCode: string }>;
}

function isPermitted(requiredRole: string | null, userRoles: string[]): boolean {
  return requiredRole === null || userRoles.includes(requiredRole);
}

/**
 * Temporary fixture-backed implementation. Reads the transition graph from
 * `workflowActionsByTenantAndStatus` (see that file's own doc comment for
 * which edges are real/enforced today versus proposed-only) and mutates
 * the shared invoice fixture store on execute, via
 * `updateFixtureInvoiceStatus` — so a successful action is visible to every
 * other fixture client reading the same invoice (the queue, the review
 * screen), not just to this one.
 */
export class FixtureWorkflowActionClient implements WorkflowActionClient {
  async getAvailableActions(tenantId: string, fromStatusCode: string, userRoles: string[]): Promise<WorkflowAction[]> {
    const rawActions = workflowActionsByTenantAndStatus[tenantId]?.[fromStatusCode] ?? [];

    return rawActions
      .filter((action) => isPermitted(action.requiredRole, userRoles))
      .map((action) => ({ targetStatusCode: action.toStatusCode, targetStatusLabel: action.label }));
  }

  async executeAction(
    tenantId: string,
    invoiceId: string,
    fromStatusCode: string,
    targetStatusCode: string,
    userRoles: string[],
  ): Promise<{ newStatusCode: string }> {
    const rawActions = workflowActionsByTenantAndStatus[tenantId]?.[fromStatusCode] ?? [];
    const matched = rawActions.find((action) => action.toStatusCode === targetStatusCode);

    if (!matched) {
      throw new Error('This action is no longer available for this invoice. Please refresh and try again.');
    }

    if (!isPermitted(matched.requiredRole, userRoles)) {
      throw new Error(
        `You do not have permission to perform this action. The "${matched.label}" action requires the ${getRoleDisplayName(matched.requiredRole!)} role.`,
      );
    }

    const updated = updateFixtureInvoiceStatus(tenantId, invoiceId, targetStatusCode);
    if (!updated) {
      throw new Error(`Invoice '${invoiceId}' was not found.`);
    }

    return { newStatusCode: targetStatusCode };
  }
}

/**
 * The client instance the app uses. Swap this single line for a real
 * HTTP-backed implementation once the backend contract is confirmed — no
 * other file needs to change.
 */
export const workflowActionClient: WorkflowActionClient = new FixtureWorkflowActionClient();
