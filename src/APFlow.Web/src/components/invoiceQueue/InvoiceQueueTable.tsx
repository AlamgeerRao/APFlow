import type { InvoiceListItem, InvoiceSortField, SortDirection } from '@/types/invoice';
import { formatCurrency, formatDate } from '@/utils/format';
import { InvoiceStatusBadge } from '@/components/invoiceQueue/InvoiceStatusBadge';
import { DuplicateIndicator } from '@/components/invoiceQueue/DuplicateIndicator';

interface InvoiceQueueTableProps {
  invoices: InvoiceListItem[];
  sortBy: InvoiceSortField;
  sortDirection: SortDirection;
  onSortChange: (field: InvoiceSortField) => void;
}

interface ColumnDef {
  field: InvoiceSortField;
  label: string;
}

const COLUMNS: ColumnDef[] = [
  { field: 'supplierName', label: 'Supplier' },
  { field: 'invoiceNumber', label: 'Invoice Number' },
  { field: 'invoiceDate', label: 'Date' },
  { field: 'amount', label: 'Amount' },
  { field: 'status', label: 'Status' },
];

function SortIcon({ direction }: { direction: SortDirection }) {
  return (
    <svg aria-hidden="true" viewBox="0 0 20 20" fill="currentColor" className="h-3.5 w-3.5">
      {direction === 'asc' ? (
        <path d="M10 4.5a.75.75 0 0 1 .624.334l4 6a.75.75 0 0 1-.624 1.166H6a.75.75 0 0 1-.624-1.166l4-6A.75.75 0 0 1 10 4.5Z" />
      ) : (
        <path d="M10 15.5a.75.75 0 0 1-.624-.334l-4-6A.75.75 0 0 1 6 8h8a.75.75 0 0 1 .624 1.166l-4 6a.75.75 0 0 1-.624.334Z" />
      )}
    </svg>
  );
}

/**
 * Read-only, sortable Invoice work queue table. Rows flagged as a
 * potential duplicate (WP-010/WP-012) are visually highlighted per
 * WP-015 task 5. No row-level actions — approval/editing/notes are
 * explicitly out of scope for this work package.
 */
export function InvoiceQueueTable({ invoices, sortBy, sortDirection, onSortChange }: InvoiceQueueTableProps) {
  if (invoices.length === 0) {
    return (
      <div className="rounded-md border border-slate-200 bg-white p-8 text-center text-sm text-slate-600">
        No invoices match the current search and filters.
      </div>
    );
  }

  return (
    <div className="overflow-x-auto rounded-md border border-slate-200 bg-white">
      <table className="min-w-full divide-y divide-slate-200 text-sm">
        <thead>
          <tr>
            {COLUMNS.map((column) => {
              const isActive = column.field === sortBy;
              return (
                <th key={column.field} scope="col" className="px-4 py-3 text-left font-medium text-slate-600">
                  <button
                    type="button"
                    onClick={() => onSortChange(column.field)}
                    className="flex items-center gap-1 rounded focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
                    aria-label={`Sort by ${column.label}${isActive ? `, currently ${sortDirection === 'asc' ? 'ascending' : 'descending'}` : ''}`}
                  >
                    {column.label}
                    {isActive && <SortIcon direction={sortDirection} />}
                  </button>
                </th>
              );
            })}
            <th scope="col" className="px-4 py-3 text-left font-medium text-slate-600">
              <span className="sr-only">Flags</span>
            </th>
          </tr>
        </thead>
        <tbody className="divide-y divide-slate-100">
          {invoices.map((invoice) => (
            <tr
              key={invoice.id}
              className={invoice.isPotentialDuplicate ? 'bg-amber-50' : undefined}
              data-testid="invoice-row"
              data-duplicate={invoice.isPotentialDuplicate}
            >
              <td className="px-4 py-3 text-ink-900">{invoice.supplierName}</td>
              <td className="px-4 py-3 text-ink-900">{invoice.invoiceNumber}</td>
              <td className="px-4 py-3 text-ink-900">{formatDate(invoice.invoiceDate)}</td>
              <td className="px-4 py-3 text-ink-900">{formatCurrency(invoice.amount, invoice.currencyCode)}</td>
              <td className="px-4 py-3">
                <InvoiceStatusBadge statusCode={invoice.status} />
              </td>
              <td className="px-4 py-3">
                {invoice.isPotentialDuplicate && <DuplicateIndicator reason={invoice.duplicateCheckReason} />}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
