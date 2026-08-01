import type { InvoiceListItem, InvoiceQueryParams, InvoiceQueryResult } from '@/types/invoice';
import { invoiceFixtures } from '@/api/fixtures/invoices.fixture';
import { httpClient } from '@/api/httpClient';

/**
 * Client-side contract for querying a page of the acting tenant's Invoice
 * work queue. WP-011's real endpoint contract was not available when
 * WP-015 was built — see docs/WP-015-Invoice-Queue-Decisions.md for the
 * proposed HTTP contract this interface is expected to map onto.
 *
 * Consumers (useInvoiceQueue) depend on this interface, not on the
 * fixture implementation below, so swapping in the real APFlow.Api-backed
 * client requires no change to any consumer.
 */
export interface InvoiceClient {
  queryInvoices(params: InvoiceQueryParams): Promise<InvoiceQueryResult>;
}

/** Exported for reuse by other fixture clients needing the same search semantics (e.g. WP-019's supplierFolderClient). */
export function matchesInvoiceSearch(invoice: InvoiceListItem, search: string): boolean {
  const term = search.trim().toLowerCase();
  if (!term) return true;
  return (
    invoice.supplierName.toLowerCase().includes(term) || invoice.invoiceNumber.toLowerCase().includes(term)
  );
}

function compareInvoices(a: InvoiceListItem, b: InvoiceListItem, params: InvoiceQueryParams): number {
  const direction = params.sortDirection === 'desc' ? -1 : 1;

  switch (params.sortBy) {
    case 'amount':
      return (a.amount - b.amount) * direction;
    case 'invoiceDate':
      // WP-072: invoiceDate is genuinely nullable - a missing date sorts as if empty,
      // rather than throwing, consistent with formatDate's own "never throw" fix.
      return (a.invoiceDate ?? '').localeCompare(b.invoiceDate ?? '') * direction;
    case 'invoiceNumber':
      return a.invoiceNumber.localeCompare(b.invoiceNumber) * direction;
    case 'status':
      return a.status.localeCompare(b.status) * direction;
    case 'supplierName':
    default:
      return a.supplierName.localeCompare(b.supplierName) * direction;
  }
}

/**
 * Temporary fixture-backed implementation, used until WP-011's real query
 * endpoint contract is confirmed. Performs the same search/filter/sort/
 * pagination steps a real backend query would, against in-memory fixture
 * data, so this logic is exercised for real rather than stubbed out.
 */
export class FixtureInvoiceClient implements InvoiceClient {
  async queryInvoices(params: InvoiceQueryParams): Promise<InvoiceQueryResult> {
    const tenantInvoices = invoiceFixtures.filter((invoice) => invoice.tenantId === params.tenantId);

    const filtered = tenantInvoices
      .filter((invoice) => (params.status ? invoice.status === params.status : true))
      .filter((invoice) => matchesInvoiceSearch(invoice, params.search ?? ''));

    const sorted = [...filtered].sort((a, b) => compareInvoices(a, b, params));

    const totalCount = sorted.length;
    const start = (params.page - 1) * params.pageSize;
    const items: InvoiceListItem[] = sorted.slice(start, start + params.pageSize).map((invoice) => ({
      id: invoice.id,
      supplierName: invoice.supplierName,
      invoiceNumber: invoice.invoiceNumber,
      invoiceDate: invoice.invoiceDate,
      amount: invoice.amount,
      currencyCode: invoice.currencyCode,
      status: invoice.status,
      isPotentialDuplicate: invoice.isPotentialDuplicate,
      duplicateCheckReason: invoice.duplicateCheckReason,
      duplicateMatchInvoiceId: invoice.duplicateMatchInvoiceId,
    }));

    return { items, totalCount, page: params.page, pageSize: params.pageSize };
  }
}

/**
 * Real DTOs for `GET /api/invoices` (WP-058), confirmed 2026-07-27 by QA
 * against the real backend source directly (`InvoiceDto.cs`, and WP-058's
 * `GetAll` endpoint parameters). This delivery originally guessed
 * `search`/`sortDirection` and `invoiceNumber`/`amount`/`currencyCode` —
 * all wrong. Fixed here — see
 * docs/WP-020-Real-Auth-And-Api-Integration-Decisions.md §7 for the full
 * correction, including the capability this fix newly exposes: the real
 * endpoint only substring-matches `invoiceNumber`, with no supplier-name
 * search at all, unlike this client's original (fixture-only) search
 * semantics.
 */
interface InvoiceListItemResponseDto {
  id: string;
  supplierName: string;
  supplierInvoiceNumber: string;
  invoiceDate: string;
  grossTotal: number;
  currency: string;
  status: string;
  isPotentialDuplicate: boolean;
  duplicateCheckReason: string | null;
  duplicateMatchInvoiceId: string | null;
}

interface InvoiceQueryResponseDto {
  items: InvoiceListItemResponseDto[];
  totalCount: number;
  page: number;
  pageSize: number;
}

function mapListItem(dto: InvoiceListItemResponseDto): InvoiceListItem {
  return {
    id: dto.id,
    supplierName: dto.supplierName,
    invoiceNumber: dto.supplierInvoiceNumber,
    invoiceDate: dto.invoiceDate,
    amount: dto.grossTotal,
    currencyCode: dto.currency,
    status: dto.status,
    isPotentialDuplicate: dto.isPotentialDuplicate,
    duplicateCheckReason: dto.duplicateCheckReason,
    duplicateMatchInvoiceId: dto.duplicateMatchInvoiceId,
  };
}

/**
 * Real HTTP-backed implementation, calling WP-058's `GET /api/invoices`
 * (WP-020 task 5). `tenantId` is deliberately NOT sent as a query
 * parameter — the API resolves the acting tenant from the caller's own
 * Bearer token (`ICurrentUserService`), never from client-supplied input;
 * accepting a client-chosen tenant would be a cross-tenant data leak.
 *
 * `params.search` is sent as `invoiceNumber` — the real endpoint has no
 * separate free-text/supplier-name search parameter, only an
 * invoice-number substring match. Supplier-name search (part of this
 * client's original, fixture-only search semantics — see
 * `matchesInvoiceSearch` above) is NOT possible through this endpoint as
 * it exists today; this is a real capability reduction versus the
 * fixture, not a bug in this mapping. `sortBy` is sent as-is (unconfirmed
 * but not contradicted by anything reviewed); `sortDirection` maps to the
 * real `sortDescending: boolean` parameter.
 */
export class HttpInvoiceClient implements InvoiceClient {
  async queryInvoices(params: InvoiceQueryParams): Promise<InvoiceQueryResult> {
    const response = await httpClient.get<InvoiceQueryResponseDto>('/api/invoices', {
      params: {
        invoiceNumber: params.search,
        status: params.status,
        sortBy: params.sortBy,
        sortDescending: params.sortDirection === 'desc' ? 'true' : 'false',
        page: params.page,
        pageSize: params.pageSize,
      },
    });

    return {
      items: response.items.map(mapListItem),
      totalCount: response.totalCount,
      page: response.page,
      pageSize: response.pageSize,
    };
  }
}

/**
 * The client instance the app uses. `HttpInvoiceClient` as of WP-020 —
 * swap this single line back to `new FixtureInvoiceClient()` if the real
 * API becomes unreachable during local development.
 */
export const invoiceClient: InvoiceClient = new HttpInvoiceClient();
