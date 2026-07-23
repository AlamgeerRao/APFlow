import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { InvoicePdfViewer } from '@/components/invoiceReview/InvoicePdfViewer';

describe('InvoicePdfViewer', () => {
  it('renders the object element pointing at the given PDF URL', () => {
    const { container } = render(<InvoicePdfViewer pdfUrl="/sample-invoices/inv-pd-001.pdf" invoiceNumber="NW-1001" />);

    const object = container.querySelector('object');
    expect(object).not.toBeNull();
    expect(object).toHaveAttribute('data', '/sample-invoices/inv-pd-001.pdf');
    expect(object).toHaveAttribute('type', 'application/pdf');
  });

  it('always shows a persistent "Open in new tab" link alongside the viewer, not only as a fallback', () => {
    render(<InvoicePdfViewer pdfUrl="/sample-invoices/inv-pd-001.pdf" invoiceNumber="NW-1001" />);

    const links = screen.getAllByRole('link', { name: /Open in new tab/i });
    expect(links.length).toBeGreaterThan(0);
    expect(links[0]).toHaveAttribute('href', '/sample-invoices/inv-pd-001.pdf');
  });

  it('always shows a persistent Download link alongside the viewer', () => {
    render(<InvoicePdfViewer pdfUrl="/sample-invoices/inv-pd-001.pdf" invoiceNumber="NW-1001" />);

    expect(screen.getByRole('link', { name: 'Download' })).toHaveAttribute(
      'href',
      '/sample-invoices/inv-pd-001.pdf',
    );
  });

  it("includes fallback content inside the object element for browsers that can't render the PDF inline", () => {
    const { container } = render(<InvoicePdfViewer pdfUrl="/sample-invoices/inv-pd-001.pdf" invoiceNumber="NW-1001" />);

    const object = container.querySelector('object');
    expect(object?.textContent).toContain("This browser can't display the PDF inline.");
  });
});
