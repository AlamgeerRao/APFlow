import type { SupplierGroup } from '@/types/supplierFolder';
import type { Supplier } from '@/types/supplier';
import { InvoiceQueueTable } from '@/components/invoiceQueue/InvoiceQueueTable';

interface SupplierGroupListProps {
  groups: SupplierGroup[];
  /**
   * WP-027: the real `Supplier` record backing each group's name, keyed by
   * `name.trim().toLowerCase()` — the same case-insensitive/trimmed
   * resolution `InvoiceProcessingService.ResolveSupplierAsync` already uses
   * when matching an extracted invoice's supplier name (WP-012), so a group
   * heading resolves to the same record the backend would. Absent when the
   * caller hasn't finished loading suppliers yet, or (rare — every invoice
   * supplier is auto-created, see WP-026's report) no match exists; either
   * way the group simply renders without an Edit action rather than
   * crashing.
   */
  suppliersByName?: Map<string, Supplier>;
  /** Opens the edit form for the resolved supplier (WP-027 task 1/2). Omitted entirely disables the per-group Edit action (e.g. while suppliers are still loading). */
  onEdit?: (supplier: Supplier) => void;
}

/**
 * Renders invoices grouped by supplier (WP-019 task 2). Each group reuses
 * the existing, already-tested `InvoiceQueueTable` (WP-015) rather than a
 * second table implementation — duplicate highlighting, status badges, and
 * click-to-review navigation all come for free and stay consistent with
 * the Invoice Queue. Sorting within a group wasn't asked for in this WP's
 * task list, so `onSortChange` is a no-op; rows are pre-sorted by date
 * (newest first) by the client.
 *
 * WP-027: an "Edit" action was added to each group's heading, resolving the
 * group's `supplierName` to a real `Supplier` record via `suppliersByName`
 * — the confirmed approach (Sprint2-Plan.md §3 WP-027, "building on the
 * existing SupplierGroupList context") for editing management fields
 * without a second, parallel supplier list.
 */
export function SupplierGroupList({ groups, suppliersByName, onEdit }: SupplierGroupListProps) {
  if (groups.length === 0) {
    return (
      <div className="rounded-md border border-slate-200 bg-white p-8 text-center text-sm text-slate-600">
        No suppliers match the current search and filters.
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      {groups.map((group) => {
        const supplier = suppliersByName?.get(group.supplierName.trim().toLowerCase());

        return (
          <section key={group.supplierName} aria-label={group.supplierName}>
            <div className="mb-2 flex items-baseline justify-between">
              <div className="flex items-baseline gap-2">
                <h3 className="text-sm font-semibold text-ink-900">{group.supplierName}</h3>
                {supplier && onEdit && (
                  <button
                    type="button"
                    onClick={() => onEdit(supplier)}
                    className="rounded-md px-1.5 py-0.5 text-xs font-medium text-accent-600 hover:bg-accent-50 hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
                  >
                    Edit
                  </button>
                )}
              </div>
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
        );
      })}
    </div>
  );
}
