import { Link } from 'react-router-dom';

export function InvoiceReviewLoadingState() {
  return (
    <div
      className="flex items-center justify-center rounded-md border border-slate-200 bg-white p-12 text-sm text-slate-600"
      role="status"
    >
      Loading invoice…
    </div>
  );
}

interface InvoiceReviewErrorStateProps {
  message: string;
  onRetry: () => void;
}

export function InvoiceReviewErrorState({ message, onRetry }: InvoiceReviewErrorStateProps) {
  return (
    <div
      className="flex flex-col items-center gap-3 rounded-md border border-red-200 bg-red-50 p-12 text-center"
      role="alert"
    >
      <p className="text-sm font-medium text-red-800">{message}</p>
      <button
        type="button"
        onClick={onRetry}
        className="rounded-md border border-red-300 bg-white px-3 py-1.5 text-sm font-medium text-red-800 hover:bg-red-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
      >
        Retry
      </button>
    </div>
  );
}

export function InvoiceReviewNotFoundState() {
  return (
    <div className="flex flex-col items-center gap-3 rounded-md border border-slate-200 bg-white p-12 text-center">
      <p className="text-sm font-medium text-ink-900">Invoice not found.</p>
      <Link
        to="/invoices"
        className="rounded-md bg-ink-900 px-3 py-1.5 text-sm font-medium text-white hover:bg-ink-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
      >
        Back to Invoice Queue
      </Link>
    </div>
  );
}
