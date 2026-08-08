import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { StatusCountsGrid } from '@/components/dashboard/StatusCountsGrid';
import type { FolderSummary } from '@/types/supplierFolder';

function renderGrid(folderCounts: FolderSummary[]) {
  return render(
    <MemoryRouter>
      <StatusCountsGrid folderCounts={folderCounts} />
    </MemoryRouter>,
  );
}

describe('StatusCountsGrid', () => {
  it('renders one tile per folder, with its count and label', () => {
    renderGrid([
      { statusCode: 'AWAITING_REVIEW', statusLabel: 'Awaiting Review', count: 3 },
      { statusCode: 'NEEDS_QUERY', statusLabel: 'Needs Query', count: 0 },
    ]);

    expect(screen.getByText('Awaiting Review')).toBeInTheDocument();
    expect(screen.getByText('3')).toBeInTheDocument();
    expect(screen.getByText('Needs Query')).toBeInTheDocument();
    expect(screen.getByText('0')).toBeInTheDocument();
  });

  it('links each tile to that status\'s kebab-case Invoice Queue route', () => {
    renderGrid([{ statusCode: 'CHECKED_READY_TO_APPROVE', statusLabel: 'Checked & Ready to Approve', count: 1 }]);

    const link = screen.getByRole('link', { name: /Checked & Ready to Approve/ });
    expect(link).toHaveAttribute('href', '/invoices/checked-ready-to-approve');
  });

  it('shows an empty state when there are no folders', () => {
    renderGrid([]);

    expect(screen.getByText(/No active invoices in any folder/)).toBeInTheDocument();
  });
});
