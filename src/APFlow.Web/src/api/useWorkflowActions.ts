import { useCallback, useEffect, useState } from 'react';
import { useAuth } from '@/auth/useAuth';
import { workflowActionClient } from '@/api/workflowActionClient';
import type { WorkflowAction } from '@/types/workflowAction';

interface WorkflowActionsState {
  actions: WorkflowAction[];
  isLoading: boolean;
  error: string | null;
  /** True while an action is being executed — disables the action buttons. */
  isExecuting: boolean;
  /** Rejection message from the most recent execute attempt, if any (WP-018 task 7). */
  executeError: string | null;
  /**
   * Executes an action. Returns true on success — the caller is
   * responsible for triggering "refresh UI after update" (task 6), e.g. by
   * calling the invoice detail page's own `retry()`, since re-fetching the
   * invoice itself (not just this hook's own action list) is what needs to
   * happen; this hook re-loads its own action list automatically once its
   * `fromStatusCode` prop changes as a result of that outer refresh.
   */
  executeAction: (action: WorkflowAction) => Promise<boolean>;
}

/**
 * Loads the actions available to the acting user for an invoice currently
 * in `fromStatusCode`, and exposes a way to execute one (WP-018).
 * Re-fetches automatically whenever `fromStatusCode` changes — including
 * after a successful execution causes the invoice's status (and therefore
 * this prop, once the caller refetches the invoice) to change, which is
 * what naturally reveals the new status's own available actions without
 * this hook needing any direct knowledge of the invoice detail fetch.
 */
export function useWorkflowActions(invoiceId: string | undefined, fromStatusCode: string | undefined): WorkflowActionsState {
  const { user } = useAuth();
  const [actions, setActions] = useState<WorkflowAction[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [isExecuting, setIsExecuting] = useState(false);
  const [executeError, setExecuteError] = useState<string | null>(null);

  useEffect(() => {
    if (!user || !fromStatusCode) {
      return;
    }

    let cancelled = false;
    // eslint-disable-next-line react-hooks/set-state-in-effect
    setIsLoading(true);
    setError(null);

    workflowActionClient
      .getAvailableActions(user.tenantId, fromStatusCode, user.roles)
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
  }, [user, fromStatusCode]);

  const executeAction = useCallback(
    async (action: WorkflowAction): Promise<boolean> => {
      if (!user || !invoiceId || !fromStatusCode) return false;

      setIsExecuting(true);
      setExecuteError(null);
      try {
        await workflowActionClient.executeAction(user.tenantId, invoiceId, fromStatusCode, action.targetStatusCode, user.roles);
        return true;
      } catch (err) {
        setExecuteError(err instanceof Error ? err.message : 'Unable to perform this action. Please try again.');
        return false;
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
