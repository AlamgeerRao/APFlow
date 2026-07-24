import type { WorkflowAction } from '@/types/workflowAction';

interface WorkflowActionButtonsProps {
  /** Already permission-filtered by the caller (`useWorkflowActions`) — every action here is both a valid transition and one this user may perform (WP-018 task 3). */
  actions: WorkflowAction[];
  onSelect: (action: WorkflowAction) => void;
  disabled: boolean;
}

/**
 * Renders exactly the buttons the acting tenant's workflow configuration
 * permits for the invoice's current status — no fixed Approve/Move to
 * Query/Mark Query Raised/Resolve Query set, no tenant-specific label
 * hardcoded into a conditional (WP-018 task 1). For GB Skips specifically,
 * "Mark Checked & Ready to Approve" and "Escalate to Febina" appear here
 * exactly when `actions` contains them — this component has no GB-Skips-
 * specific branching of its own; the tenant-specific behaviour lives
 * entirely in the data it's given (see `workflowActions.fixture.ts`).
 */
export function WorkflowActionButtons({ actions, onSelect, disabled }: WorkflowActionButtonsProps) {
  if (actions.length === 0) {
    return <p className="text-sm text-slate-600">No actions are available for this invoice's current status.</p>;
  }

  return (
    <div className="flex flex-wrap gap-2">
      {actions.map((action) => (
        <button
          key={action.targetStatusCode}
          type="button"
          disabled={disabled}
          onClick={() => onSelect(action)}
          className="rounded-md border border-slate-200 bg-white px-3 py-1.5 text-sm font-medium text-ink-900 hover:bg-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600 disabled:cursor-not-allowed disabled:text-slate-400"
        >
          {action.targetStatusLabel}
        </button>
      ))}
    </div>
  );
}
