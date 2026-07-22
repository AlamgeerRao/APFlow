/**
 * Client-side shape for a row in the Invoice work queue, and the query
 * contract used to search/filter/sort/paginate it.
 *
 * PROVISIONAL CONTRACT: WP-011 (Invoice Repository & Query Services) has
 * already been implemented on the backend, but its report/API contract
 * was not available when WP-015 was built. This shape is a reasoned
 * proposal, documented in docs/WP-015-Invoice-Queue-Decisions.md and
 * flagged there for Chief Technical Architect / backend confirmation. It
 * only uses field names already confirmed by the WP-012 report
 * (isPotentialDuplicate, duplicateCheckReason) plus additive, minimal
 * fields needed for the columns WP-015 explicitly asks for.
 */

/** A single invoice row as displayed in the work queue. */
export interface InvoiceListItem {
  id: string;
  supplierName: string;
  invoiceNumber: string;
  /** ISO 8601 date string, e.g. "2026-07-18". */
  invoiceDate: string;
  amount: number;
  /** ISO 4217 currency code, e.g. "GBP". */
  currencyCode: string;
  /** StatusReference.code for the acting tenant's WorkflowTemplate (WP-050). */
  status: string;
  isPotentialDuplicate: boolean;
  duplicateCheckReason: string | null;
}

export type InvoiceSortField = 'supplierName' | 'invoiceNumber' | 'invoiceDate' | 'amount' | 'status';
export type SortDirection = 'asc' | 'desc';

/** Query parameters for a single page of the Invoice work queue. */
export interface InvoiceQueryParams {
  tenantId: string;
  /** Free-text match against supplier name and invoice number. */
  search?: string;
  /** Optional single StatusReference.code to narrow the queue to. */
  status?: string;
  sortBy: InvoiceSortField;
  sortDirection: SortDirection;
  /** 1-based page number. */
  page: number;
  pageSize: number;
}

export interface InvoiceQueryResult {
  items: InvoiceListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
}
