import { useEffect, useState } from 'react';
import type { WorkflowTemplate } from '@/types/workflowTemplate';
import { workflowTemplateClient } from '@/api/workflowTemplateClient';
import { useAuth } from '@/auth/useAuth';

interface WorkflowTemplateState {
  template: WorkflowTemplate | null;
  isLoading: boolean;
  error: string | null;
}

/**
 * Loads the acting tenant's Invoice WorkflowTemplate so nav (and later,
 * queue/status UI) can render against tenant-specific data rather than a
 * hardcoded list, per WP-014 task 2.
 */
export function useWorkflowTemplate(): WorkflowTemplateState {
  const { user } = useAuth();
  const [state, setState] = useState<WorkflowTemplateState>({
    template: null,
    isLoading: true,
    error: null,
  });

  useEffect(() => {
    if (!user) {
      setState({ template: null, isLoading: false, error: null });
      return;
    }

    let cancelled = false;
    setState((previous) => ({ ...previous, isLoading: true, error: null }));

    workflowTemplateClient
      .getCurrentWorkflowTemplate(user.tenantId)
      .then((template) => {
        if (!cancelled) {
          setState({ template, isLoading: false, error: null });
        }
      })
      .catch(() => {
        if (!cancelled) {
          setState({
            template: null,
            isLoading: false,
            error: 'Unable to load workflow configuration for this tenant.',
          });
        }
      });

    return () => {
      cancelled = true;
    };
  }, [user]);

  return state;
}
