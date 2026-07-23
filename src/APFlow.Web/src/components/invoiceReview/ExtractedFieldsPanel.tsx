import type { ExtractedField } from '@/types/invoiceDetail';
import { ConfidenceBadge } from '@/components/invoiceReview/ConfidenceBadge';

interface ExtractedFieldsPanelProps {
  fields: ExtractedField[];
}

/** Raw Document Intelligence extraction results, each with its own confidence score (WP-016 task 2). */
export function ExtractedFieldsPanel({ fields }: ExtractedFieldsPanelProps) {
  return (
    <section aria-labelledby="extracted-fields-heading" className="rounded-md border border-slate-200 bg-white p-4">
      <h2 id="extracted-fields-heading" className="mb-3 text-sm font-semibold text-ink-900">
        Extracted Fields
      </h2>
      {fields.length === 0 ? (
        <p className="text-sm text-slate-600">No extracted fields available for this invoice.</p>
      ) : (
        <ul className="divide-y divide-slate-100">
          {fields.map((field) => (
            <li key={field.fieldKey} className="flex items-center justify-between gap-4 py-2">
              <div className="min-w-0">
                <p className="text-xs font-medium uppercase tracking-wide text-slate-400">{field.label}</p>
                <p className="truncate text-sm text-ink-900">{field.value}</p>
              </div>
              <ConfidenceBadge score={field.confidenceScore} />
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
