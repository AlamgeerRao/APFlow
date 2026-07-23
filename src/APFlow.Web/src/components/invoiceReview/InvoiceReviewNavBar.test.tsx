import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import type { ComponentProps } from 'react';
import { InvoiceReviewNavBar } from '@/components/invoiceReview/InvoiceReviewNavBar';

function renderNavBar(props: Partial<ComponentProps<typeof InvoiceReviewNavBar>>) {
  return render(
    <MemoryRouter>
      <InvoiceReviewNavBar previousId={null} nextId={null} position={null} total={null} {...props} />
    </MemoryRouter>,
  );
}

describe('InvoiceReviewNavBar', () => {
  it('renders Previous as a real link when a previousId is given', () => {
    renderNavBar({ previousId: 'inv-1' });

    const link = screen.getByRole('link', { name: /Previous/i });
    expect(link).toHaveAttribute('href', '/invoices/review/inv-1');
  });

  it('renders Previous as disabled (non-link) when there is no previousId', () => {
    renderNavBar({ previousId: null });

    expect(screen.queryByRole('link', { name: /Previous/i })).not.toBeInTheDocument();
    expect(screen.getByText('← Previous')).toHaveAttribute('aria-disabled', 'true');
  });

  it('renders Next as a real link when a nextId is given', () => {
    renderNavBar({ nextId: 'inv-2' });

    const link = screen.getByRole('link', { name: /Next/i });
    expect(link).toHaveAttribute('href', '/invoices/review/inv-2');
  });

  it('renders Next as disabled (non-link) when there is no nextId', () => {
    renderNavBar({ nextId: null });

    expect(screen.queryByRole('link', { name: /Next/i })).not.toBeInTheDocument();
    expect(screen.getByText('Next →')).toHaveAttribute('aria-disabled', 'true');
  });

  it('shows the position indicator when position and total are provided', () => {
    renderNavBar({ position: 3, total: 12 });

    expect(screen.getByText('3 of 12')).toBeInTheDocument();
  });
});
