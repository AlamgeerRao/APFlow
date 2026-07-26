import type { SupplierGroup } from '@/types/supplierFolder';
import { InvoiceQueueTable } from '@/components/invoiceQueue/InvoiceQueueTable';

interface SupplierGroupListProps {
  groups: SupplierGroup[];
}

/**
 * Renders invoices grouped by supplier (WP-019 task 2). Each group reuses
 * the existing, already-tested `InvoiceQueueTable` (WP-015) rather than a
 * second table implementation — duplicate highlighting, status badges, and
 * click-to-review navigation all come for free and stay consistent with
 * the Invoice Queue. Sorting within a group wasn't asked for in this WP's
 * task list, so `onSortChange` is a no-op; rows are pre-sorted by date
 * (newest first) by the client.
 */
export function SupplierGroupList({ groups }: SupplierGroupListProps) {
  if (groups.length === 0) {
    return (
      <div className="rounded-md border border-slate-200 bg-white p-8 text-center text-sm text-slate-600">
        No suppliers match the current search and filters.
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      {groups.map((group) => (
        <section key={group.supplierName} aria-label={group.supplierName}>
          <div className="mb-2 flex items-baseline justify-between">
            <h3 className="text-sm font-semibold text-ink-900">{group.supplierName}</h3>
            <span className="text-xs text-slate-400">
              {group.count} {group.count === 1 ? 'invoice' : 'invoices'}
            </span>
          </div>
          <InvoiceQueueTable
            invoices={group.invoices}
            sortBy="invoiceDate"
            sortDirection="desc"
            onSortChange={() => {}}
          />
        </section>
      ))}
    </div>
  );
}
