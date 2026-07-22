import type { InvoiceListItem, InvoiceQueryParams, InvoiceQueryResult } from '@/types/invoice';
import { invoiceFixtures } from '@/api/fixtures/invoices.fixture';

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

function matchesSearch(invoice: InvoiceListItem, search: string): boolean {
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
      return a.invoiceDate.localeCompare(b.invoiceDate) * direction;
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
      .filter((invoice) => matchesSearch(invoice, params.search ?? ''));

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
    }));

    return { items, totalCount, page: params.page, pageSize: params.pageSize };
  }
}

/**
 * The client instance the app uses. Swap this single line for a real
 * HTTP-backed implementation once WP-011's contract is confirmed — no
 * other file needs to change.
 */
export const invoiceClient: InvoiceClient = new FixtureInvoiceClient();
