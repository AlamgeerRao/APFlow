import type { WorkflowAction } from '@/types/workflowAction';
import type { InvoiceDetail } from '@/types/invoiceDetail';
import { workflowActionsByTenantAndStatus } from '@/api/fixtures/workflowActions.fixture';
import { updateFixtureInvoiceStatus } from '@/api/fixtures/invoices.fixture';
import { getRoleDisplayName } from '@/utils/roles';
import { httpClient } from '@/api/httpClient';
import { mapInvoiceDetailResponse, type InvoiceDetailResponseDto } from '@/api/invoiceDetailMapping';
import { FixtureInvoiceDetailClient } from '@/api/invoiceDetailClient';

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
   * Returns the actions available to THIS acting user, for the given
   * invoice (currently in `fromStatusCode`) — already filtered by
   * permission (WP-018 task 3: "query the user's permission before
   * rendering the button, not just before submitting"). An action absent
   * from this list is not merely hidden — as far as this client's caller
   * is concerned, it does not exist as an option for this user right now.
   *
   * Takes `invoiceId` because WP-054's real endpoint is per-invoice
   * (`GET /api/invoices/{id}/available-actions`), not per-status — a gap
   * discovered during the WP-020 swap: WP-018's original interface only
   * took `fromStatusCode`, since the fixture could compute the answer
   * from status/role alone. `fromStatusCode` is kept (used by the fixture
   * implementation; unused by the real one, which asks the invoice
   * itself) rather than removed, since removing it would require
   * `useWorkflowActions` to always have an `InvoiceDetail` on hand before
   * it can ask this question, which isn't guaranteed at every call site.
   */
  getAvailableActions(
    tenantId: string,
    invoiceId: string,
    fromStatusCode: string,
    userRoles: string[],
  ): Promise<WorkflowAction[]>;

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
   *
   * Returns the full updated `InvoiceDetail` (minus `pdfUrl` — see
   * invoiceDetailMapping.ts's own doc comment for why), per WP-054's real
   * contract and docs/Backlog.md's explicit instruction to use it
   * directly rather than a second `retry()` round trip. The caller
   * (`WorkflowActionsPanel`) merges in the invoice's already-resolved
   * `pdfUrl` before applying this to `useInvoiceDetail`'s state.
   */
  executeAction(
    tenantId: string,
    invoiceId: string,
    fromStatusCode: string,
    targetStatusCode: string,
    userRoles: string[],
    notes?: string | null,
  ): Promise<Omit<InvoiceDetail, 'pdfUrl'>>;
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
  async getAvailableActions(
    tenantId: string,
    _invoiceId: string,
    fromStatusCode: string,
    userRoles: string[],
  ): Promise<WorkflowAction[]> {
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
  ): Promise<Omit<InvoiceDetail, 'pdfUrl'>> {
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

    const detail = await new FixtureInvoiceDetailClient().getInvoiceDetail(tenantId, invoiceId);
    if (!detail) {
      throw new Error(
        `Invoice '${invoiceId}' has no fixture detail record — this fixture client only supports full-detail returns for invoices seeded in invoiceDetails.fixture.ts.`,
      );
    }
    return {
      id: detail.id,
      supplierName: detail.supplierName,
      invoiceNumber: detail.invoiceNumber,
      invoiceDate: detail.invoiceDate,
      amount: detail.amount,
      currencyCode: detail.currencyCode,
      status: targetStatusCode,
      isPotentialDuplicate: detail.isPotentialDuplicate,
      duplicateCheckReason: detail.duplicateCheckReason,
      duplicateMatchInvoiceId: detail.duplicateMatchInvoiceId,
      sourceDocumentBlobName: detail.sourceDocumentBlobName,
      receivedAt: detail.receivedAt,
      extractedFields: detail.extractedFields,
      overallConfidenceScore: detail.overallConfidenceScore,
      auditEntries: detail.auditEntries,
    };
  }
}

/**
 * Real HTTP-backed implementation (WP-020 tasks 5–6), calling WP-054's
 * `GET /api/invoices/{id}/available-actions` and
 * `PATCH /api/invoices/{id}/status`. Field names already match exactly —
 * `{ targetStatusCode, targetStatusLabel }[]` — no mapping needed for
 * `getAvailableActions`, confirmed in docs/Backlog.md against the real,
 * deployed endpoint.
 *
 * `tenantId`/`userRoles` are accepted for interface compatibility but
 * unused here: the API resolves both the acting tenant and the caller's
 * roles from the Bearer token server-side (the same reason
 * `HttpInvoiceClient` doesn't send `tenantId` as a query param) — a
 * client-supplied role list would be meaningless to trust for a
 * server-side permission decision anyway.
 */
export class HttpWorkflowActionClient implements WorkflowActionClient {
  async getAvailableActions(
    _tenantId: string,
    invoiceId: string,
    _fromStatusCode: string,
    _userRoles: string[],
  ): Promise<WorkflowAction[]> {
    return httpClient.get<WorkflowAction[]>(`/api/invoices/${invoiceId}/available-actions`);
  }

  async executeAction(
    _tenantId: string,
    invoiceId: string,
    _fromStatusCode: string,
    targetStatusCode: string,
    _userRoles: string[],
    notes?: string | null,
  ): Promise<Omit<InvoiceDetail, 'pdfUrl'>> {
    const response = await httpClient.patch<InvoiceDetailResponseDto>(`/api/invoices/${invoiceId}/status`, {
      targetStatusCode,
      notes: notes ?? null,
    });
    return mapInvoiceDetailResponse(response);
  }
}

/**
 * The client instance the app uses. `HttpWorkflowActionClient` as of
 * WP-020 — swap this single line back to `new FixtureWorkflowActionClient()`
 * if the real API becomes unreachable during local development.
 */
export const workflowActionClient: WorkflowActionClient = new HttpWorkflowActionClient();
