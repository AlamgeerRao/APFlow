export function InvoiceQueueLoadingState() {
  return (
    <div
      className="flex items-center justify-center rounded-md border border-slate-200 bg-white p-8 text-sm text-slate-600"
      role="status"
    >
      Loading invoices…
    </div>
  );
}

interface InvoiceQueueErrorStateProps {
  message: string;
  onRetry: () => void;
}

export function InvoiceQueueErrorState({ message, onRetry }: InvoiceQueueErrorStateProps) {
  return (
    <div className="flex flex-col items-center gap-3 rounded-md border border-red-200 bg-red-50 p-8 text-center" role="alert">
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
