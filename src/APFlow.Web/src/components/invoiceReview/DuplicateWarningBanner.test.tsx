import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { DuplicateWarningBanner } from '@/components/invoiceReview/DuplicateWarningBanner';

describe('DuplicateWarningBanner', () => {
  it('renders as an alert with the given reason', () => {
    render(<DuplicateWarningBanner reason="All fields matched an existing invoice." />);

    const alert = screen.getByRole('alert');
    expect(alert).toHaveTextContent('Possible duplicate invoice');
    expect(alert).toHaveTextContent('All fields matched an existing invoice.');
  });

  it('falls back to a generic message when no reason is provided', () => {
    render(<DuplicateWarningBanner reason={null} />);

    expect(screen.getByRole('alert')).toHaveTextContent(
      'This invoice matched an existing invoice during duplicate detection.',
    );
  });
});
