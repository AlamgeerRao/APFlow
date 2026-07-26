import type { InvoiceListItem } from '@/types/invoice';

/**
 * Client-side shape for WP-019 (Supplier & Folder Views).
 *
 * PROVISIONAL CONTRACT: no real backend endpoint exists for a folder-count
 * summary or supplier-grouped invoice listing — same situation WP-015/016/
 * 017/018 hit. See docs/WP-019-Supplier-Folder-Views-Decisions.md for the
 * proposed (non-binding) HTTP contract this is expected to map onto.
 */

/** A single workflow-status "folder" with its invoice count for the current search context. */
export interface FolderSummary {
  statusCode: string;
  statusLabel: string;
  count: number;
}

/** One supplier's invoices within the current folder/search/supplier-filter context. */
export interface SupplierGroup {
  supplierName: string;
  count: number;
  invoices: InvoiceListItem[];
}

export interface SupplierFolderQueryParams {
  tenantId: string;
  /** Optional single StatusReference.code to narrow to one folder. Omitted = every non-terminal status. */
  folder?: string;
  /** Optional exact supplier name to narrow to. */
  supplier?: string;
  /** Free-text match against supplier name and invoice number (same semantics as WP-015's search). */
  search?: string;
  /** 1-based page number, paginating over supplier groups (not individual invoices). */
  page: number;
  pageSize: number;
}

export interface SupplierFolderQueryResult {
  groups: SupplierGroup[];
  totalSuppliers: number;
  page: number;
  pageSize: number;
}
