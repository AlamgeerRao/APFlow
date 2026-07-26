import '@testing-library/jest-dom/vitest';

// jsdom doesn't implement URL.createObjectURL/revokeObjectURL at all.
// HttpInvoiceDetailClient uses these for the PDF-auth blob-URL workaround
// (see invoiceDetailClient.ts's doc comment) — stub them so tests can
// exercise that code path without a real Blob-URL implementation.
if (typeof URL.createObjectURL !== 'function') {
  URL.createObjectURL = () => `blob:mock-${Math.random().toString(36).slice(2)}`;
}
if (typeof URL.revokeObjectURL !== 'function') {
  URL.revokeObjectURL = () => {};
}
