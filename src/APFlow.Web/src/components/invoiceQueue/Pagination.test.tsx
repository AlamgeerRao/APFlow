import { describe, expect, it, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { Pagination } from '@/components/invoiceQueue/Pagination';

describe('Pagination', () => {
  it('disables Previous on the first page and enables Next when more pages remain', () => {
    render(<Pagination page={1} pageSize={10} totalCount={25} onPageChange={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Next' })).toBeEnabled();
    expect(screen.getByText('Showing 1–10 of 25')).toBeInTheDocument();
  });

  it('disables Next on the last page and enables Previous', () => {
    render(<Pagination page={3} pageSize={10} totalCount={25} onPageChange={vi.fn()} />);

    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Previous' })).toBeEnabled();
    expect(screen.getByText('Showing 21–25 of 25')).toBeInTheDocument();
  });

  it('calls onPageChange with page + 1 when Next is clicked', async () => {
    const onPageChange = vi.fn();
    const user = userEvent.setup();
    render(<Pagination page={1} pageSize={10} totalCount={25} onPageChange={onPageChange} />);

    await user.click(screen.getByRole('button', { name: 'Next' }));

    expect(onPageChange).toHaveBeenCalledWith(2);
  });

  it('calls onPageChange with page - 1 when Previous is clicked', async () => {
    const onPageChange = vi.fn();
    const user = userEvent.setup();
    render(<Pagination page={2} pageSize={10} totalCount={25} onPageChange={onPageChange} />);

    await user.click(screen.getByRole('button', { name: 'Previous' }));

    expect(onPageChange).toHaveBeenCalledWith(1);
  });

  it('shows a "No results" message and disables both buttons when totalCount is 0', () => {
    render(<Pagination page={1} pageSize={10} totalCount={0} onPageChange={vi.fn()} />);

    expect(screen.getByText('No results')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled();
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled();
  });
});
