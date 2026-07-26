import type {
  FolderSummary,
  SupplierFolderQueryParams,
  SupplierFolderQueryResult,
  SupplierGroup,
} from '@/types/supplierFolder';
import type { InvoiceListItem } from '@/types/invoice';
import { invoiceFixtures } from '@/api/fixtures/invoices.fixture';
import { matchesInvoiceSearch } from '@/api/invoiceClient';
import { workflowTemplateClient } from '@/api/workflowTemplateClient';

/**
 * Client-side contract for WP-019's Supplier & Folder Views. Consumers
 * (useSupplierFolderView) depend on this interface, not the fixture
 * implementation below, so swapping in a real HTTP client is a one-line
 * change (`supplierFolderClient` below).
 *
 * STILL ON FIXTURES AS OF WP-020: unlike WP-015/016/017/018, no backend
 * endpoint exists anywhere for this WP's three operations
 * (folder-count summary, supplier-name listing, or supplier-grouped
 * invoice listing) — status-postwb-057.md §2.2's live API surface table
 * lists only `GET /api/invoices` (list), `GET /api/invoices/{id}`
 * (detail), `available-actions`, `status`, `download`, and `notes`.
 * Nothing supports grouping-by-supplier or a folder-count summary at all.
 * WP-020 task 5 asked to swap this WP's client too, but doing so would
 * mean inventing the very endpoints this client's own proposed contract
 * (see docs/WP-019-Supplier-Folder-Views-Decisions.md §1) only proposed
 * non-bindingly — exactly the kind of business/API-contract invention
 * `02_Project_Standards.md` §7 prohibits. Flagged as a new backlog item
 * (docs/WP-020-Real-Auth-And-Api-Integration-Decisions.md §4) rather than
 * silently left unswapped without explanation.
 */
export interface SupplierFolderClient {
  /** Per-folder invoice counts for the tenant's non-terminal statuses, honouring the current search text. */
  getFolderCounts(tenantId: string, search?: string): Promise<FolderSummary[]>;
  /** Distinct supplier names available for the current folder/search context, for the supplier filter dropdown. */
  getSuppliers(tenantId: string, folder?: string, search?: string): Promise<string[]>;
  /** A page of supplier-grouped invoices for the current folder/supplier/search context. */
  getGroupedInvoices(params: SupplierFolderQueryParams): Promise<SupplierFolderQueryResult>;
}

type FixtureInvoice = InvoiceListItem & { tenantId: string };

function toListItem(invoice: FixtureInvoice): InvoiceListItem {
  return {
    id: invoice.id,
    supplierName: invoice.supplierName,
    invoiceNumber: invoice.invoiceNumber,
    invoiceDate: invoice.invoiceDate,
    amount: invoice.amount,
    currencyCode: invoice.currencyCode,
    status: invoice.status,
    isPotentialDuplicate: invoice.isPotentialDuplicate,
    duplicateCheckReason: invoice.duplicateCheckReason,
  };
}

/**
 * Temporary fixture-backed implementation, built on the same
 * `invoiceFixtures` array WP-015/018 already use (so a status change made
 * via WP-018's workflow actions is immediately reflected here too), and
 * the same tenant `WorkflowTemplate` fixtures WP-014 uses for the folder
 * list — no separate, possibly-divergent fixture data invented for this
 * WP.
 */
export class FixtureSupplierFolderClient implements SupplierFolderClient {
  async getFolderCounts(tenantId: string, search = ''): Promise<FolderSummary[]> {
    const template = await workflowTemplateClient.getCurrentWorkflowTemplate(tenantId);
    const matching = invoiceFixtures.filter(
      (invoice) => invoice.tenantId === tenantId && matchesInvoiceSearch(invoice, search),
    );

    return [...template.statuses]
      .filter((status) => !status.isTerminal)
      .sort((a, b) => a.order - b.order)
      .map((status) => ({
        statusCode: status.code,
        statusLabel: status.name,
        count: matching.filter((invoice) => invoice.status === status.code).length,
      }));
  }

  async getSuppliers(tenantId: string, folder?: string, search = ''): Promise<string[]> {
    const matching = invoiceFixtures.filter(
      (invoice) =>
        invoice.tenantId === tenantId &&
        (folder ? invoice.status === folder : true) &&
        matchesInvoiceSearch(invoice, search),
    );

    return [...new Set(matching.map((invoice) => invoice.supplierName))].sort((a, b) => a.localeCompare(b));
  }

  async getGroupedInvoices(params: SupplierFolderQueryParams): Promise<SupplierFolderQueryResult> {
    const matching = invoiceFixtures.filter(
      (invoice) =>
        invoice.tenantId === params.tenantId &&
        (params.folder ? invoice.status === params.folder : true) &&
        (params.supplier ? invoice.supplierName === params.supplier : true) &&
        matchesInvoiceSearch(invoice, params.search ?? ''),
    );

    const bySupplier = new Map<string, InvoiceListItem[]>();
    for (const invoice of matching) {
      const existing = bySupplier.get(invoice.supplierName) ?? [];
      existing.push(toListItem(invoice));
      bySupplier.set(invoice.supplierName, existing);
    }

    const allGroups: SupplierGroup[] = [...bySupplier.entries()]
      .map(([supplierName, invoices]) => ({
        supplierName,
        count: invoices.length,
        invoices: [...invoices].sort((a, b) => b.invoiceDate.localeCompare(a.invoiceDate)),
      }))
      .sort((a, b) => a.supplierName.localeCompare(b.supplierName));

    const totalSuppliers = allGroups.length;
    const start = (params.page - 1) * params.pageSize;
    const groups = allGroups.slice(start, start + params.pageSize);

    return { groups, totalSuppliers, page: params.page, pageSize: params.pageSize };
  }
}

/**
 * The client instance the app uses. Swap this single line for a real
 * HTTP-backed implementation once a backend contract is confirmed — no
 * other file needs to change.
 */
export const supplierFolderClient: SupplierFolderClient = new FixtureSupplierFolderClient();
