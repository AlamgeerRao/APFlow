import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { DuplicateWarningBanner } from '@/components/invoiceReview/DuplicateWarningBanner';

// WP-073 added a "View matching invoice" Link, so every render now needs a
// Router context (Link throws without one).
function renderBanner(reason: string | null, duplicateMatchInvoiceId: string | null) {
  return render(
    <MemoryRouter>
      <DuplicateWarningBanner reason={reason} duplicateMatchInvoiceId={duplicateMatchInvoiceId} />
    </MemoryRouter>,
  );
}

describe('DuplicateWarningBanner', () => {
  it('renders as an alert with the given reason', () => {
    renderBanner('All fields matched an existing invoice.', 'inv-2');

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Possible duplicate invoice');
    expect(alert).toHaveTextContent('All fields matched an existing invoice.');
  });

  it('falls back to a generic message when no reason is provided', () => {
    renderBanner(null, 'inv-2');

    expect(screen.getByRole('alert')).toHaveTextContent(
      'This invoice matched an existing invoice during duplicate detection.',
    );
  });

  // WP-073 task 5: the reason text renders as-is, plus a link to the matched invoice.
  it('renders a "View matching invoice" link pointing at the matched invoice when an id is given', () => {
    renderBanner('Matches an existing invoice on Supplier and Invoice Number (\'2W4WVCTZ-0001\').', 'inv-matched-id');

    const link = screen.getByRole('link', { name: /View matching invoice/i });
    expect(link).toHaveAttribute('href', '/invoices/review/inv-matched-id');
  });

  // WP-073 task 6: older, already-flagged invoices won't retroactively have this
  // field - the banner must still render correctly, just without the link.
  it('omits the link, without breaking, when duplicateMatchInvoiceId is null', () => {
    renderBanner('All fields matched an existing invoice.', null);

    expect(screen.getByRole('alert')).toHaveTextContent('All fields matched an existing invoice.');
    expect(screen.queryByRole('link', { name: /View matching invoice/i })).not.toBeInTheDocument();
  });
});
