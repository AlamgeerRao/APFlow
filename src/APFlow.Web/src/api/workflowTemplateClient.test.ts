import { describe, expect, it } from 'vitest';
import { FixtureWorkflowTemplateClient } from '@/api/workflowTemplateClient';
import { platformDefaultWorkflowTemplate } from '@/api/fixtures/platformDefault.workflowTemplate';
import { gbSkipsWorkflowTemplate } from '@/api/fixtures/gbSkips.workflowTemplate';

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
