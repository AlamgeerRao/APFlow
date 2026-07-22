import { describe, expect, it } from 'vitest';
import type { WorkflowTemplate } from '@/types/workflowTemplate';
import { buildInvoiceQueueLinks, buildNavSections, STATIC_NAV_SECTIONS } from '@/components/layout/navConfig';

function template(overrides: Partial<WorkflowTemplate> = {}): WorkflowTemplate {
  return {
    tenantId: 'test-tenant',
    templateName: 'Test Tenant',
    statuses: [
      { code: 'RECEIVED', name: 'Received', isTerminal: false, order: 1 },
      { code: 'ARCHIVED', name: 'Archived', isTerminal: true, order: 3 },
      { code: 'AWAITING_REVIEW', name: 'Awaiting Review', isTerminal: false, order: 2 },
    ],
    ...overrides,
  };
}

describe('buildInvoiceQueueLinks', () => {
  it('excludes terminal statuses', () => {
    const links = buildInvoiceQueueLinks(template());

    expect(links.some((link) => link.key === 'ARCHIVED')).toBe(false);
  });

  it('orders non-terminal statuses ascending by StatusReference.order, regardless of input order', () => {
    const links = buildInvoiceQueueLinks(template());

    expect(links.map((link) => link.key)).toEqual(['RECEIVED', 'AWAITING_REVIEW']);
  });

  it('does not mutate the template.statuses array it was given', () => {
    const source = template();
    const originalOrder = source.statuses.map((s) => s.code);

    buildInvoiceQueueLinks(source);

    expect(source.statuses.map((s) => s.code)).toEqual(originalOrder);
  });

  it('derives a kebab-case path from each status code', () => {
    const links = buildInvoiceQueueLinks(template());
    const awaitingReview = links.find((link) => link.key === 'AWAITING_REVIEW');

    expect(awaitingReview?.path).toBe('/invoices/awaiting-review');
  });

  it('returns an empty array when every status is terminal', () => {
    const links = buildInvoiceQueueLinks(
      template({ statuses: [{ code: 'ARCHIVED', name: 'Archived', isTerminal: true, order: 1 }] }),
    );

    expect(links).toEqual([]);
  });
});

describe('buildNavSections', () => {
  it('returns every static top-level section unchanged when no template is available', () => {
    const sections = buildNavSections(null);

    expect(sections.map((s) => s.key)).toEqual(STATIC_NAV_SECTIONS.map((s) => s.key));
    const invoiceQueue = sections.find((s) => s.key === 'invoice-queue');
    expect(invoiceQueue?.children).toBeUndefined();
  });

  it('attaches Invoice Queue children only, leaving every other section without children', () => {
    const sections = buildNavSections(template());

    for (const section of sections) {
      if (section.key === 'invoice-queue') {
        expect(section.children?.length).toBeGreaterThan(0);
      } else {
        expect(section.children).toBeUndefined();
      }
    }
  });
});
