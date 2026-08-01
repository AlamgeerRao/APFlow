import { describe, expect, it, vi, beforeEach } from 'vitest';
import { FixtureIngestionIssueClient, HttpIngestionIssueClient } from '@/api/ingestionIssueClient';
import { ingestionIssueFixtures } from '@/api/fixtures/ingestionIssues.fixture';
import { httpClient } from '@/api/httpClient';

vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn() } };
});

describe('FixtureIngestionIssueClient', () => {
  const client = new FixtureIngestionIssueClient();

  it('returns every fixture issue, most recently seen first', async () => {
    const result = await client.queryIngestionIssues(1, 25);

    expect(result.totalCount).toBe(ingestionIssueFixtures.length);
    expect(result.items[0].lastSeenUtc >= result.items[1].lastSeenUtc).toBe(true);
  });

  it('paginates', async () => {
    const result = await client.queryIngestionIssues(1, 1);

    expect(result.items).toHaveLength(1);
    expect(result.totalCount).toBe(ingestionIssueFixtures.length);
    expect(result.page).toBe(1);
    expect(result.pageSize).toBe(1);
  });
});

describe('HttpIngestionIssueClient', () => {
  beforeEach(() => {
    vi.mocked(httpClient.get).mockReset();
  });

  it('calls GET /api/ingestion-issues with page/pageSize, no tenantId', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({ items: [], totalCount: 0, page: 1, pageSize: 25 });
    const client = new HttpIngestionIssueClient();

    await client.queryIngestionIssues(1, 25);

    expect(httpClient.get).toHaveBeenCalledWith('/api/ingestion-issues', { params: { page: 1, pageSize: 25 } });
  });

  it('maps the real IngestionIssueDto field names', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({
      items: [
        {
          id: 'issue-1',
          senderAddress: 'vendor@example.com',
          senderName: 'Vendor Co',
          subject: 'No invoice here',
          firstSeenUtc: '2026-07-20T09:12:00Z',
          lastSeenUtc: '2026-07-22T08:05:00Z',
          occurrenceCount: 3,
          reasonCode: 'NO_PROCESSABLE_ATTACHMENTS',
          attachmentsFound: 'receipt.png (image/png)',
        },
      ],
      totalCount: 1,
      page: 1,
      pageSize: 25,
    });
    const client = new HttpIngestionIssueClient();

    const result = await client.queryIngestionIssues(1, 25);

    expect(result.items).toEqual([
      {
        id: 'issue-1',
        senderAddress: 'vendor@example.com',
        senderName: 'Vendor Co',
        subject: 'No invoice here',
        firstSeenUtc: '2026-07-20T09:12:00Z',
        lastSeenUtc: '2026-07-22T08:05:00Z',
        occurrenceCount: 3,
        reasonCode: 'NO_PROCESSABLE_ATTACHMENTS',
        attachmentsFound: 'receipt.png (image/png)',
      },
    ]);
    expect(result.totalCount).toBe(1);
  });
});
