import type { InvoiceDetail } from '@/types/invoiceDetail';
import { invoiceDetailFixtures } from '@/api/fixtures/invoiceDetails.fixture';

/**
 * Client-side contract for fetching a single invoice's full detail for
 * the Review Screen (WP-016). WP-009/WP-008/WP-013's real contracts were
 * not available when this was built — see
 * docs/WP-016-Invoice-Review-Decisions.md for the proposed shape.
 */
export interface InvoiceDetailClient {
  getInvoiceDetail(tenantId: string, invoiceId: string): Promise<InvoiceDetail | null>;
}

/**
 * Temporary fixture-backed implementation. Only a handful of fixture
 * invoices have full detail records (see invoiceDetails.fixture.ts);
 * requesting any other id resolves to null, which the page treats as
 * "not found" rather than an error.
 */
export class FixtureInvoiceDetailClient implements InvoiceDetailClient {
  async getInvoiceDetail(tenantId: string, invoiceId: string): Promise<InvoiceDetail | null> {
    const match = invoiceDetailFixtures.find(
      (invoice) => invoice.tenantId === tenantId && invoice.id === invoiceId,
    );
    if (!match) return null;

    const detail: InvoiceDetail = {
      id: match.id,
      supplierName: match.supplierName,
      invoiceNumber: match.invoiceNumber,
      invoiceDate: match.invoiceDate,
      amount: match.amount,
      currencyCode: match.currencyCode,
      status: match.status,
      isPotentialDuplicate: match.isPotentialDuplicate,
      duplicateCheckReason: match.duplicateCheckReason,
      pdfUrl: match.pdfUrl,
      sourceDocumentBlobName: match.sourceDocumentBlobName,
      receivedAt: match.receivedAt,
      extractedFields: match.extractedFields,
      overallConfidenceScore: match.overallConfidenceScore,
      auditEntries: match.auditEntries,
    };
    return detail;
  }
}

/**
 * The client instance the app uses. Swap this single line for a real
 * HTTP-backed implementation once the backend contract is confirmed — no
 * other file needs to change.
 */
export const invoiceDetailClient: InvoiceDetailClient = new FixtureInvoiceDetailClient();
