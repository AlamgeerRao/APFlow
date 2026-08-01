/**
 * Client-side shape for an unprocessable-email record (WP-076) - the Inbox's
 * "flagged" list. Mirrors `IngestionIssueDto` (`IngestionIssueDto.cs`).
 */
export interface IngestionIssue {
  id: string;
  senderAddress: string;
  senderName: string | null;
  subject: string;
  /** ISO 8601 timestamp - when the first occurrence of this issue was received. */
  firstSeenUtc: string;
  /** ISO 8601 timestamp - when the most recent occurrence was recorded. */
  lastSeenUtc: string;
  occurrenceCount: number;
  reasonCode: string;
  attachmentsFound: string;
}

export interface IngestionIssueQueryResult {
  items: IngestionIssue[];
  totalCount: number;
  page: number;
  pageSize: number;
}
