import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { IngestionIssuesTable } from '@/components/ingestion/IngestionIssuesTable';
import type { IngestionIssue } from '@/types/ingestionIssue';

function issue(overrides: Partial<IngestionIssue> = {}): IngestionIssue {
  return {
    id: 'issue-1',
    senderAddress: 'vendor@example.com',
    senderName: 'Vendor Co',
    subject: 'No invoice here',
    firstSeenUtc: '2026-07-20T09:12:00Z',
    lastSeenUtc: '2026-07-20T09:12:00Z',
    occurrenceCount: 1,
    reasonCode: 'NO_PROCESSABLE_ATTACHMENTS',
    attachmentsFound: '(none)',
    ...overrides,
  };
}

describe('IngestionIssuesTable', () => {
  it('shows an empty state when there are no issues', () => {
    render(<IngestionIssuesTable issues={[]} />);

    expect(screen.getByText(/no flagged emails/i)).toBeInTheDocument();
  });

  it('renders sender, subject, and first-seen date', () => {
    render(<IngestionIssuesTable issues={[issue()]} />);

    expect(screen.getByText('Vendor Co')).toBeInTheDocument();
    expect(screen.getByText('No invoice here')).toBeInTheDocument();
    expect(screen.getByText('20 Jul 2026')).toBeInTheDocument();
  });

  it('falls back to the sender address when no sender name is known', () => {
    render(<IngestionIssuesTable issues={[issue({ senderName: null })]} />);

    expect(screen.getByText('vendor@example.com')).toBeInTheDocument();
  });

  it('shows the occurrence count only when greater than 1', () => {
    render(<IngestionIssuesTable issues={[issue({ occurrenceCount: 1 })]} />);
    expect(screen.queryByText(/×/)).not.toBeInTheDocument();
  });

  it('shows the occurrence count badge when greater than 1', () => {
    render(<IngestionIssuesTable issues={[issue({ occurrenceCount: 3 })]} />);
    expect(screen.getByText('×3')).toBeInTheDocument();
  });
});
