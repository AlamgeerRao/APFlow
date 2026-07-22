import { useWorkflowTemplate } from '@/api/useWorkflowTemplate';

interface InvoiceQueueFiltersProps {
  search: string;
  onSearchChange: (value: string) => void;
  status: string | undefined;
  onStatusChange: (value: string | undefined) => void;
}

/**
 * Search box and status filter for the Invoice work queue. The status
 * options come from the acting tenant's WorkflowTemplate (WP-050), not a
 * hardcoded list, consistent with WP-014's nav.
 */
export function InvoiceQueueFilters({
  search,
  onSearchChange,
  status,
  onStatusChange,
}: InvoiceQueueFiltersProps) {
  const { template } = useWorkflowTemplate();
  const statusOptions = [...(template?.statuses ?? [])]
    .filter((s) => !s.isTerminal)
    .sort((a, b) => a.order - b.order);

  return (
    <div className="mb-4 flex flex-col gap-3 sm:flex-row sm:items-center">
      <div className="flex-1">
        <label htmlFor="invoice-search" className="sr-only">
          Search by supplier or invoice number
        </label>
        <input
          id="invoice-search"
          type="search"
          value={search}
          onChange={(event) => onSearchChange(event.target.value)}
          placeholder="Search by supplier or invoice number…"
          className="w-full rounded-md border border-slate-200 px-3 py-2 text-sm focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
        />
      </div>

      <div>
        <label htmlFor="invoice-status-filter" className="sr-only">
          Filter by status
        </label>
        <select
          id="invoice-status-filter"
          value={status ?? ''}
          onChange={(event) => onStatusChange(event.target.value === '' ? undefined : event.target.value)}
          className="w-full rounded-md border border-slate-200 px-3 py-2 text-sm focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600 sm:w-56"
        >
          <option value="">All statuses</option>
          {statusOptions.map((option) => (
            <option key={option.code} value={option.code}>
              {option.name}
            </option>
          ))}
        </select>
      </div>
    </div>
  );
}
