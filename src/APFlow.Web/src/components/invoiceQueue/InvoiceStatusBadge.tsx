import { useWorkflowTemplate } from '@/api/useWorkflowTemplate';

interface InvoiceStatusBadgeProps {
  statusCode: string;
}

/**
 * Renders an invoice's status using the acting tenant's WorkflowTemplate
 * display name where available, falling back to the raw status code if
 * the template hasn't loaded yet or doesn't (yet) recognise the code.
 */
export function InvoiceStatusBadge({ statusCode }: InvoiceStatusBadgeProps) {
  const { template } = useWorkflowTemplate();
  const matched = template?.statuses.find((status) => status.code === statusCode);

  return (
    <span className="inline-flex items-center rounded-full bg-slate-100 px-2.5 py-0.5 text-xs font-medium text-ink-700">
      {matched?.name ?? statusCode}
    </span>
  );
}
