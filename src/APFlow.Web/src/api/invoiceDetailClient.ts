import type { InvoiceDetail } from '@/types/invoiceDetail';
import { invoiceDetailFixtures } from '@/api/fixtures/invoiceDetails.fixture';
import { httpClient, ApiError } from '@/api/httpClient';
import { mapInvoiceDetailResponse, type InvoiceDetailResponseDto } from '@/api/invoiceDetailMapping';

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
      duplicateMatchInvoiceId: match.duplicateMatchInvoiceId,
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
 * Real HTTP-backed implementation (WP-020 task 5), calling
 * `GET /api/invoices/{id}` and, for the PDF specifically,
 * `GET /api/invoices/{id}/download`.
 *
 * The download endpoint requires the same Bearer token every other
 * endpoint does — but the native `<object>`/`<a>` elements
 * `InvoicePdfViewer` uses (Architect-approved Option A,
 * docs/WP-016-Invoice-Review-Decisions.md §1) can't attach an
 * Authorization header to a plain URL load. Resolved here, not in
 * `InvoicePdfViewer`: fetch the PDF as an authenticated Blob via
 * `httpClient.getBlob`, then hand the viewer a same-origin `blob:` object
 * URL instead of the raw API URL. `InvoicePdfViewer` and
 * `InvoiceReviewPage` needed no changes at all — both already just treat
 * `invoice.pdfUrl` as "a URL the browser can load directly," which a
 * `blob:` URL satisfies exactly as well as the fixture client's static
 * asset path did. `useInvoiceDetail.ts` revokes the previous object URL
 * when a new one is created or the hook unmounts, to avoid leaking one
 * per Prev/Next navigation.
 */
export class HttpInvoiceDetailClient implements InvoiceDetailClient {
  async getInvoiceDetail(_tenantId: string, invoiceId: string): Promise<InvoiceDetail | null> {
    let response: InvoiceDetailResponseDto;
    try {
      response = await httpClient.get<InvoiceDetailResponseDto>(`/api/invoices/${invoiceId}`);
    } catch (error) {
      if (error instanceof ApiError && error.status === 404) {
        return null;
      }
      throw error;
    }

    // A missing/undownloadable PDF (e.g. no document was ever attached, or a
    // transient Blob Storage failure) shouldn't fail the whole invoice detail
    // page - the invoice record itself already loaded successfully above.
    // InvoicePdfViewer renders its own "unavailable" message for a null
    // pdfUrl instead.
    let pdfUrl: string | null = null;
    try {
      const pdfBlob = await httpClient.getBlob(`/api/invoices/${invoiceId}/download`);
      pdfUrl = URL.createObjectURL(pdfBlob);
    } catch {
      pdfUrl = null;
    }

    return { ...mapInvoiceDetailResponse(response), pdfUrl };
  }
}

/**
 * The client instance the app uses. `HttpInvoiceDetailClient` as of
 * WP-020 — swap this single line back to `new FixtureInvoiceDetailClient()`
 * if the real API becomes unreachable during local development.
 */
export const invoiceDetailClient: InvoiceDetailClient = new HttpInvoiceDetailClient();
