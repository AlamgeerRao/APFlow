import { describe, expect, it } from 'vitest';
import { FixtureInvoiceDetailClient } from '@/api/invoiceDetailClient';

describe('FixtureInvoiceDetailClient', () => {
  const client = new FixtureInvoiceDetailClient();

  it('returns full detail for a known invoice in the correct tenant', async () => {
    const result = await client.getInvoiceDetail('platform-default', 'inv-pd-001');

    expect(result).not.toBeNull();
    expect(result?.invoiceNumber).toBe('NW-1001');
    expect(result?.extractedFields.length).toBeGreaterThan(0);
    expect(result?.auditEntries.length).toBeGreaterThan(0);
    expect(result?.pdfUrl).toMatch(/\.pdf$/);
  });

  it('does not leak the internal tenantId field onto the returned detail', async () => {
    const result = await client.getInvoiceDetail('platform-default', 'inv-pd-001');

    expect(result).not.toHaveProperty('tenantId');
  });

  it('returns null when the invoice id exists but belongs to a different tenant', async () => {
    const result = await client.getInvoiceDetail('gb-skips', 'inv-pd-001');

    expect(result).toBeNull();
  });

  it('returns null for an unknown invoice id', async () => {
    const result = await client.getInvoiceDetail('platform-default', 'does-not-exist');

    expect(result).toBeNull();
  });

  it('carries the isPotentialDuplicate/duplicateCheckReason fields through unchanged', async () => {
    const result = await client.getInvoiceDetail('platform-default', 'inv-pd-002');

    expect(result?.isPotentialDuplicate).toBe(true);
    expect(result?.duplicateCheckReason).toBeTruthy();
  });
});
