interface InvoicePdfViewerProps {
  pdfUrl: string;
  invoiceNumber: string;
}

/**
 * Displays the invoice PDF using the browser's built-in PDF renderer
 * (Architect-approved Option A — see docs/WP-016-Invoice-Review-Decisions.md
 * §1). No PDF library dependency.
 *
 * Renders via <object>, whose children act as the browser-native fallback
 * content if inline PDF rendering isn't available. A persistent "Open in
 * new tab" / "Download" link is also always shown alongside the viewer
 * (not only as a fallback), per the Architect's implementation note —
 * some corporate/locked-down browsers disable the PDF plugin without
 * reliably triggering <object>'s fallback rendering.
 */
export function InvoicePdfViewer({ pdfUrl, invoiceNumber }: InvoicePdfViewerProps) {
  return (
    <section aria-labelledby="pdf-viewer-heading" className="rounded-md border border-slate-200 bg-white p-4">
      <div className="mb-3 flex items-center justify-between">
        <h2 id="pdf-viewer-heading" className="text-sm font-semibold text-ink-900">
          Source Document
        </h2>
        <div className="flex gap-3 text-sm">
          <a
            href={pdfUrl}
            target="_blank"
            rel="noopener noreferrer"
            className="font-medium text-accent-600 hover:text-accent-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
          >
            Open in new tab
          </a>
          <a
            href={pdfUrl}
            download
            className="font-medium text-accent-600 hover:text-accent-700 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-accent-600"
          >
            Download
          </a>
        </div>
      </div>

      <object
        data={pdfUrl}
        type="application/pdf"
        aria-label={`PDF for invoice ${invoiceNumber}`}
        className="h-[70vh] w-full rounded border border-slate-200"
      >
        <div className="flex h-full flex-col items-center justify-center gap-2 p-6 text-center text-sm text-slate-600">
          <p>This browser can't display the PDF inline.</p>
          <a href={pdfUrl} target="_blank" rel="noopener noreferrer" className="font-medium text-accent-600 hover:text-accent-700">
            Open PDF in a new tab
          </a>
        </div>
      </object>
    </section>
  );
}
