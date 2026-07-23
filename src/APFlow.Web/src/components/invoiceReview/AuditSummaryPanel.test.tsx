import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AuditSummaryPanel } from '@/components/invoiceReview/AuditSummaryPanel';

describe('AuditSummaryPanel', () => {
  it('renders each audit entry with its action, description, and actor', () => {
    render(
      <AuditSummaryPanel
        entries={[
          {
            id: 'audit-1',
            timestamp: '2026-07-01T08:12:00Z',
            actor: 'System',
            action: 'Received',
            description: 'Invoice received via email ingestion.',
          },
        ]}
      />,
    );

    expect(screen.getByText('Received')).toBeInTheDocument();
    expect(screen.getByText('Invoice received via email ingestion.')).toBeInTheDocument();
    expect(screen.getByText(/System/)).toBeInTheDocument();
  });

  it('shows an empty-state message when there is no audit history', () => {
    render(<AuditSummaryPanel entries={[]} />);

    expect(screen.getByText(/No audit history available/i)).toBeInTheDocument();
  });
});
