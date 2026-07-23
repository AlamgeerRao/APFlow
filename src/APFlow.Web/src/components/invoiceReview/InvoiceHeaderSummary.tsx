import type { ReactNode } from 'react';
import type { InvoiceDetail } from '@/types/invoiceDetail';
import { formatCurrency, formatDate } from '@/utils/format';
import { InvoiceStatusBadge } from '@/components/invoiceQueue/InvoiceStatusBadge';
import { ConfidenceBadge } from '@/components/invoiceReview/ConfidenceBadge';

interface InvoiceHeaderSummaryProps {
  invoice: InvoiceDetail;
}

interface FieldProps {
  label: string;
  value: ReactNode;
}

function Field({ label, value }: FieldProps) {
  return (
    <div>
      <dt className="text-xs font-medium uppercase tracking-wide text-slate-400">{label}</dt>
      <dd className="mt-0.5 text-sm text-ink-900">{value}</dd>
    </div>
  );
}

/** Complete, canonical invoice details (WP-016 task 1) plus the overall processing confidence score (task 5). */
export function InvoiceHeaderSummary({ invoice }: InvoiceHeaderSummaryProps) {
  return (
    <section aria-labelledby="invoice-summary-heading" className="rounded-md border border-slate-200 bg-white p-4">
      <div className="mb-3 flex items-center justify-between">
        <h2 id="invoice-summary-heading" className="text-sm font-semibold text-ink-900">
          Invoice Details
        </h2>
        <ConfidenceBadge score={invoice.overallConfidenceScore} label="Overall confidence" />
      </div>
      <dl className="grid grid-cols-2 gap-4 sm:grid-cols-3">
        <Field label="Supplier" value={invoice.supplierName} />
        <Field label="Invoice Number" value={invoice.invoiceNumber} />
        <Field label="Invoice Date" value={formatDate(invoice.invoiceDate)} />
        <Field label="Amount" value={formatCurrency(invoice.amount, invoice.currencyCode)} />
        <Field label="Status" value={<InvoiceStatusBadge statusCode={invoice.status} />} />
        <Field label="Received" value={formatDate(invoice.receivedAt.slice(0, 10))} />
      </dl>
    </section>
  );
}
