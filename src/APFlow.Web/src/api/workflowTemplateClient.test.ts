import { describe, expect, it, vi, beforeEach } from 'vitest';
import { FixtureWorkflowTemplateClient, HttpWorkflowTemplateClient } from '@/api/workflowTemplateClient';
import { platformDefaultWorkflowTemplate } from '@/api/fixtures/platformDefault.workflowTemplate';
import { gbSkipsWorkflowTemplate } from '@/api/fixtures/gbSkips.workflowTemplate';
import { httpClient } from '@/api/httpClient';

vi.mock('@/api/httpClient', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/httpClient')>();
  return { ...actual, httpClient: { ...actual.httpClient, get: vi.fn() } };
});

describe('FixtureWorkflowTemplateClient', () => {
  const client = new FixtureWorkflowTemplateClient();

  it('returns the platform-default template for the platform-default tenantId', async () => {
    const result = await client.getCurrentWorkflowTemplate('platform-default');

    expect(result).toBe(platformDefaultWorkflowTemplate);
  });

  it('returns the GB Skips template for the gb-skips tenantId', async () => {
    const result = await client.getCurrentWorkflowTemplate('gb-skips');

    expect(result).toBe(gbSkipsWorkflowTemplate);
  });

  it('falls back to the platform-default template for an unrecognised tenantId', async () => {
    const result = await client.getCurrentWorkflowTemplate('some-future-tenant-not-yet-onboarded');

    expect(result).toBe(platformDefaultWorkflowTemplate);
  });

  it('falls back to the platform-default template for an empty tenantId', async () => {
    const result = await client.getCurrentWorkflowTemplate('');

    expect(result).toBe(platformDefaultWorkflowTemplate);
  });
});

describe('HttpWorkflowTemplateClient', () => {
  beforeEach(() => {
    vi.mocked(httpClient.get).mockReset();
  });

  it('calls GET /api/workflow-template with no query params, not a tenantId', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({
      id: 'template-1',
      domainName: 'Invoice',
      name: 'GB Skips',
      isTenantSpecific: true,
      statuses: [],
      transitions: [],
    });
    const client = new HttpWorkflowTemplateClient();

    await client.getCurrentWorkflowTemplate('gb-skips');

    expect(httpClient.get).toHaveBeenCalledWith('/api/workflow-template');
  });

  // WP-075: the real field is sortOrder, not order - and the wire response has
  // no tenantId at all (the API resolves it from the caller's own token).
  it('maps the real WorkflowTemplateDto field names (sortOrder, no tenantId) to our shape', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({
      id: 'template-1',
      domainName: 'Invoice',
      name: 'GB Skips',
      isTenantSpecific: true,
      statuses: [
        { code: 'AWAITING_REVIEW', name: 'Awaiting Review', isTerminal: false, sortOrder: 4 },
        { code: 'CHECKED_READY_TO_APPROVE', name: 'Checked & Ready to Approve', isTerminal: false, sortOrder: 5 },
        { code: 'NEEDS_REVIEW_FEBINA', name: 'Needs Review by Febina', isTerminal: false, sortOrder: 6 },
        { code: 'APPROVED', name: 'Approved', isTerminal: false, sortOrder: 7 },
      ],
      transitions: [],
    });
    const client = new HttpWorkflowTemplateClient();

    const result = await client.getCurrentWorkflowTemplate('gb-skips');

    expect(result.tenantId).toBe('gb-skips');
    expect(result.templateName).toBe('GB Skips');
    expect(result.statuses).toEqual([
      { code: 'AWAITING_REVIEW', name: 'Awaiting Review', isTerminal: false, order: 4 },
      { code: 'CHECKED_READY_TO_APPROVE', name: 'Checked & Ready to Approve', isTerminal: false, order: 5 },
      { code: 'NEEDS_REVIEW_FEBINA', name: 'Needs Review by Febina', isTerminal: false, order: 6 },
      { code: 'APPROVED', name: 'Approved', isTerminal: false, order: 7 },
    ]);
  });

  // WP-075's own root cause, guarded against regressing: GB Skips' two extra
  // statuses must round-trip through the real mapping, positioned correctly.
  it('preserves both GB Skips-only statuses, positioned between AWAITING_REVIEW and APPROVED', async () => {
    vi.mocked(httpClient.get).mockResolvedValueOnce({
      id: 'template-1',
      domainName: 'Invoice',
      name: 'GB Skips',
      isTenantSpecific: true,
      statuses: [
        { code: 'AWAITING_REVIEW', name: 'Awaiting Review', isTerminal: false, sortOrder: 4 },
        { code: 'CHECKED_READY_TO_APPROVE', name: 'Checked & Ready to Approve', isTerminal: false, sortOrder: 5 },
        { code: 'NEEDS_REVIEW_FEBINA', name: 'Needs Review by Febina', isTerminal: false, sortOrder: 6 },
        { code: 'APPROVED', name: 'Approved', isTerminal: false, sortOrder: 7 },
      ],
      transitions: [],
    });
    const client = new HttpWorkflowTemplateClient();

    const result = await client.getCurrentWorkflowTemplate('gb-skips');
    const codesInOrder = result.statuses.slice().sort((a, b) => a.order - b.order).map((s) => s.code);
    const awaitingReviewIndex = codesInOrder.indexOf('AWAITING_REVIEW');
    const approvedIndex = codesInOrder.indexOf('APPROVED');

    expect(codesInOrder.indexOf('CHECKED_READY_TO_APPROVE')).toBeGreaterThan(awaitingReviewIndex);
    expect(codesInOrder.indexOf('CHECKED_READY_TO_APPROVE')).toBeLessThan(approvedIndex);
    expect(codesInOrder.indexOf('NEEDS_REVIEW_FEBINA')).toBeGreaterThan(awaitingReviewIndex);
    expect(codesInOrder.indexOf('NEEDS_REVIEW_FEBINA')).toBeLessThan(approvedIndex);
  });
});

describe('tenant workflow template fixtures (regression guard against 06_Domain_Reference_Data.md §2)', () => {
  it('GB Skips template contains exactly the two tenant-specific additions, positioned between AWAITING_REVIEW and APPROVED', () => {
    const codesInOrder = gbSkipsWorkflowTemplate.statuses
      .slice()
      .sort((a, b) => a.order - b.order)
      .map((s) => s.code);

    const awaitingReviewIndex = codesInOrder.indexOf('AWAITING_REVIEW');
    const approvedIndex = codesInOrder.indexOf('APPROVED');

    expect(codesInOrder).toContain('CHECKED_READY_TO_APPROVE');
    expect(codesInOrder).toContain('NEEDS_REVIEW_FEBINA');
    expect(codesInOrder.indexOf('CHECKED_READY_TO_APPROVE')).toBeGreaterThan(awaitingReviewIndex);
    expect(codesInOrder.indexOf('CHECKED_READY_TO_APPROVE')).toBeLessThan(approvedIndex);
    expect(codesInOrder.indexOf('NEEDS_REVIEW_FEBINA')).toBeGreaterThan(awaitingReviewIndex);
    expect(codesInOrder.indexOf('NEEDS_REVIEW_FEBINA')).toBeLessThan(approvedIndex);
  });

  it('platform-default template does not include either GB Skips-only state', () => {
    const codes = platformDefaultWorkflowTemplate.statuses.map((s) => s.code);

    expect(codes).not.toContain('CHECKED_READY_TO_APPROVE');
    expect(codes).not.toContain('NEEDS_REVIEW_FEBINA');
  });
});
