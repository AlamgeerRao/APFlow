import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '@/auth/useAuth';
import { workflowActionClient } from '@/api/workflowActionClient';
import type { WorkflowAction } from '@/types/workflowAction';
import type { InvoiceDetail } from '@/types/invoiceDetail';

interface WorkflowActionsState {
  actions: WorkflowAction[];
  isLoading: boolean;
  error: string | null;
  /** True while an action is being executed — disables the action buttons. */
  isExecuting: boolean;
  /** Rejection message from the most recent execute attempt, if any (WP-018 task 7). */
  executeError: string | null;
  /**
   * Executes an action. Returns the full updated invoice detail (minus
   * `pdfUrl` — see invoiceDetailMapping.ts) on success, or null on
   * failure (see `executeError` for the message). WP-020: the real
   * PATCH endpoint already returns the updated invoice, so the caller no
   * longer needs to trigger a separate `retry()` — see
   * `WorkflowActionsPanel`'s `onStatusChanged`.
   * `notes` (WP-084) is required by the real API for every transition -
   * this hook does not itself validate that (the dialog already refuses
   * to call this with an empty note; the server is the actual source of
   * truth) - it's threaded straight through to `workflowActionClient`,
   * which creates the linked note atomically with the status change.
   */
  executeAction: (action: WorkflowAction, notes: string) => Promise<Omit<InvoiceDetail, 'pdfUrl'> | null>;
}

/**
 * Loads the actions available to the acting user for a given invoice
 * currently in `fromStatusCode`, and exposes a way to execute one
 * (WP-018). Re-fetches automatically whenever `fromStatusCode` changes —
 * including after a successful execution causes the invoice's status
 * (and therefore this prop, once the caller applies the returned detail)
 * to change, which is what naturally reveals the new status's own
 * available actions without this hook needing any direct knowledge of
 * the invoice detail fetch.
 */
export function useWorkflowActions(invoiceId: string | undefined, fromStatusCode: string | undefined): WorkflowActionsState {
  const { user } = useAuth();
  const [actions, setActions] = useState<WorkflowAction[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isExecuting, setIsExecuting] = useState(false);
  const [executeError, setExecuteError] = useState<string | null>(null);

  useEffect(() => {
    if (!user || !invoiceId || !fromStatusCode) {
      return;
    }

    let cancelled = false;
    setIsLoading(true);
    setError(null);

    workflowActionClient
      .getAvailableActions(user.tenantId, invoiceId, fromStatusCode, user.roles)
      .then((result) => {
        if (cancelled) return;
        setActions(result);
        setIsLoading(false);
      })
      .catch(() => {
        if (!cancelled) {
          setError('Unable to load the available actions for this invoice.');
          setIsLoading(false);
        }
      });

    return () => {
      cancelled = true;
    };
  }, [user, invoiceId, fromStatusCode]);

  const executeAction = useCallback(
    async (action: WorkflowAction, notes: string): Promise<Omit<InvoiceDetail, 'pdfUrl'> | null> => {
      if (!user || !invoiceId || !fromStatusCode) return null;

      setIsExecuting(true);
      setExecuteError(null);
      try {
        return await workflowActionClient.executeAction(
          user.tenantId,
          invoiceId,
          fromStatusCode,
          action.targetStatusCode,
          user.roles,
          notes,
        );
      } catch (err) {
        setExecuteError(err instanceof Error ? err.message : 'Unable to perform this action. Please try again.');
        return null;
      } finally {
        setIsExecuting(false);
      }
    },
    [user, invoiceId, fromStatusCode],
  );

  const noSubject = !user || !invoiceId || !fromStatusCode;

  return {
    actions: noSubject ? [] : actions,
    isLoading: noSubject ? false : isLoading,
    error: noSubject ? null : error,
    isExecuting,
    executeError,
    executeAction,
  };
}
