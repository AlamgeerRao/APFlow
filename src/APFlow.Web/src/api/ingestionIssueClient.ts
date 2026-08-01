import type { IngestionIssue, IngestionIssueQueryResult } from '@/types/ingestionIssue';
import { ingestionIssueFixtures } from '@/api/fixtures/ingestionIssues.fixture';
import { httpClient } from '@/api/httpClient';

/**
 * Client-side contract for a page of the acting tenant's ingestion issues
 * (WP-076) - the Inbox's data source. Consumers (`useIngestionIssues`)
 * depend on this interface, not on the fixture implementation below.
 */
export interface IngestionIssueClient {
  queryIngestionIssues(page: number, pageSize: number): Promise<IngestionIssueQueryResult>;
}

/** Fixture-backed implementation, kept only for its own unit test coverage - see `HttpIngestionIssueClient` below for what the app actually uses. */
export class FixtureIngestionIssueClient implements IngestionIssueClient {
  async queryIngestionIssues(page: number, pageSize: number): Promise<IngestionIssueQueryResult> {
    const sorted = [...ingestionIssueFixtures].sort((a, b) => b.lastSeenUtc.localeCompare(a.lastSeenUtc));
    const start = (page - 1) * pageSize;
    return {
      items: sorted.slice(start, start + pageSize),
      totalCount: sorted.length,
      page,
      pageSize,
    };
  }
}

/** Real DTO for `GET /api/ingestion-issues` (WP-076). Field names confirmed directly against `IngestionIssueDto.cs`/`PagedResult<T>`. */
interface IngestionIssueResponseDto {
  id: string;
  senderAddress: string;
  senderName: string | null;
  subject: string;
  firstSeenUtc: string;
  lastSeenUtc: string;
  occurrenceCount: number;
  reasonCode: string;
  attachmentsFound: string;
}

interface IngestionIssuePagedResponseDto {
  items: IngestionIssueResponseDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

function fromDto(dto: IngestionIssueResponseDto): IngestionIssue {
  return {
    id: dto.id,
    senderAddress: dto.senderAddress,
    senderName: dto.senderName,
    subject: dto.subject,
    firstSeenUtc: dto.firstSeenUtc,
    lastSeenUtc: dto.lastSeenUtc,
    occurrenceCount: dto.occurrenceCount,
    reasonCode: dto.reasonCode,
    attachmentsFound: dto.attachmentsFound,
  };
}

/**
 * Real HTTP-backed implementation (WP-076), calling `GET /api/ingestion-issues`.
 * No `tenantId` parameter - same reasoning as every other real client in this
 * codebase: the API resolves the acting tenant from the caller's own Bearer
 * token, never from client-supplied input.
 */
export class HttpIngestionIssueClient implements IngestionIssueClient {
  async queryIngestionIssues(page: number, pageSize: number): Promise<IngestionIssueQueryResult> {
    const response = await httpClient.get<IngestionIssuePagedResponseDto>('/api/ingestion-issues', {
      params: { page, pageSize },
    });

    return {
      items: response.items.map(fromDto),
      totalCount: response.totalCount,
      page: response.page,
      pageSize: response.pageSize,
    };
  }
}

/** The client instance the app uses. `HttpIngestionIssueClient` as of WP-076 - swap this single line back to `new FixtureIngestionIssueClient()` if the real API becomes unreachable during local development. */
export const ingestionIssueClient: IngestionIssueClient = new HttpIngestionIssueClient();
