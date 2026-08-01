import type { IngestionIssue } from '@/types/ingestionIssue';

/** Seed ingestion issues for FixtureIngestionIssueClient - one single-occurrence row and one repeated (>1) row, so both Inbox display cases are exercised. */
export const ingestionIssueFixtures: IngestionIssue[] = [
  {
    id: 'issue-001',
    senderAddress: 'accounts@example-supplier.co.uk',
    senderName: 'Example Supplier Accounts',
    subject: 'Your statement is attached',
    firstSeenUtc: '2026-07-20T09:12:00Z',
    lastSeenUtc: '2026-07-20T09:12:00Z',
    occurrenceCount: 1,
    reasonCode: 'NO_PROCESSABLE_ATTACHMENTS',
    attachmentsFound: 'statement.png (image/png)',
  },
  {
    id: 'issue-002',
    senderAddress: 'billing@another-vendor.com',
    senderName: null,
    subject: 'Re: Re: Invoice query',
    firstSeenUtc: '2026-07-18T15:40:00Z',
    lastSeenUtc: '2026-07-22T08:05:00Z',
    occurrenceCount: 3,
    reasonCode: 'NO_PROCESSABLE_ATTACHMENTS',
    attachmentsFound: '(none)',
  },
];
