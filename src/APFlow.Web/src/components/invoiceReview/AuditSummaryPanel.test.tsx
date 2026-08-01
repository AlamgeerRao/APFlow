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

  // WP-072 follow-up: a real invoice's audit history reached this with an
  // undefined timestamp (upstream field-name mismatch, now fixed in
  // invoiceDetailMapping.ts) and crashed the whole Review screen with
  // RangeError: Invalid time value. This stays defensive regardless.
  it('renders a fallback instead of throwing when the timestamp is invalid', () => {
    render(
      <AuditSummaryPanel
        entries={[
          {
            id: 'audit-bad',
            timestamp: undefined as unknown as string,
            actor: 'system',
            action: 'DocumentViewed',
            description: 'Document viewed',
          },
        ]}
      />,
    );

    expect(screen.getByText('Document viewed')).toBeInTheDocument();
    expect(screen.getByText('—', { exact: false })).toBeInTheDocument();
  });
});
