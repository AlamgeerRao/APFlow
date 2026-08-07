import type { Supplier } from '@/types/supplier';

interface SuppliersWithoutInvoicesProps {
  suppliers: Supplier[];
  onEdit: (supplier: Supplier) => void;
}

/**
 * WP-093: a supplier with zero invoices never appears in the
 * invoice-grouped list below (`SupplierGroupList`) - not just off the
 * current page, but never, since that list is entirely invoice-driven.
 * Without this, a supplier created via "+ Add supplier" appears to vanish
 * immediately, even though it's genuinely saved.
 *
 * Deliberately rendered unaffected by the page's folder/search filters: a
 * zero-invoice supplier belongs to no folder and matches no invoice-content
 * search either way, so filtering this section by them would make the
 * supplier disappear again under the exact filters a user might reach for.
 */
export function SuppliersWithoutInvoices({ suppliers, onEdit }: SuppliersWithoutInvoicesProps) {
  if (suppliers.length === 0) {
    return null;
  }

  return (
    <div className="mb-4 rounded-md border border-slate-200 bg-white p-4">
      <h3 className="mb-2 text-sm font-semibold text-ink-900">Suppliers with no invoices yet</h3>
      <ul className="flex flex-col gap-1">
        {suppliers.map((supplier) => (
          <li key={supplier.id} className="flex items-center justify-between gap-2 text-sm text-ink-900">
            <span>{supplier.name}</span>
            <button
              type="button"
              onClick={() => onEdit(supplier)}
              className="rounded-md px-1.5 py-0.5 text-xs font-medium text-accent-600 hover:bg-accent-50 hover:underline focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
            >
              Edit
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}
