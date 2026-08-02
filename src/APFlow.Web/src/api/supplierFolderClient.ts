import type {
  FolderSummary,
  SupplierFolderQueryParams,
  SupplierFolderQueryResult,
  SupplierGroup,
} from '@/types/supplierFolder';
import type { InvoiceListItem } from '@/types/invoice';
import { invoiceFixtures } from '@/api/fixtures/invoices.fixture';
import { matchesInvoiceSearch } from '@/api/invoiceClient';
import { FixtureWorkflowTemplateClient } from '@/api/workflowTemplateClient';
import { httpClient } from '@/api/httpClient';

/**
 * WP-075: uses its own fixture instance rather than the shared
 * `workflowTemplateClient` singleton - that singleton is now
 * `HttpWorkflowTemplateClient` (real network calls), and this class exists
 * purely as a self-contained, network-free fixture for its own unit tests
 * (see the class doc comment below). Reaching through the shared singleton
 * only ever worked by coincidence, while it happened to also be
 * fixture-backed - WP-075 breaking that coincidence is exactly why this is
 * now explicit instead.
 */
const fixtureWorkflowTemplateClient = new FixtureWorkflowTemplateClient();

/**
 * Client-side contract for WP-019's Supplier & Folder Views. Consumers
 * (useSupplierFolderView) depend on this interface, not the fixture
 * implementation below, so swapping in a real HTTP client is a one-line
 * change (`supplierFolderClient` below).
 *
 * WP-059 Part A delivered the real backend endpoints this WP's three
 * operations map onto (`GET /api/invoices/folders`, `/suppliers`,
 * `/grouped`); WP-065 swaps this client over to them (see
 * `HttpSupplierFolderClient` below). `FixtureSupplierFolderClient` is kept
 * only for its existing unit tests.
 */
export interface SupplierFolderClient {
  /** Per-folder invoice counts for the tenant's non-terminal statuses, honouring the current search text. */
  getFolderCounts(tenantId: string, search?: string): Promise<FolderSummary[]>;
  /** Distinct supplier names available for the current folder/search context, for the supplier filter dropdown. */
  getSuppliers(tenantId: string, folder?: string, search?: string): Promise<string[]>;
  /** A page of supplier-grouped invoices for the current folder/supplier/search context. */
  getGroupedInvoices(params: SupplierFolderQueryParams): Promise<SupplierFolderQueryResult>;
}

/** WP-077: fallback label for a fixture invoice with a null supplierName - see getSuppliers/getGroupedInvoices. */
const UNKNOWN_SUPPLIER_NAME = '(Unknown Supplier)';

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
    duplicateMatchInvoiceId: invoice.duplicateMatchInvoiceId,
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
    const template = await fixtureWorkflowTemplateClient.getCurrentWorkflowTemplate(tenantId);
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

    // WP-077: supplierName is genuinely nullable on InvoiceListItem, but
    // SupplierGroup.supplierName (this method's own return shape) is not -
    // matches the real SupplierGroupDto.SupplierName contract, which the
    // backend never sends null (see supplierName below). A fallback label
    // keeps that contract true here too rather than grouping every
    // unresolved-supplier invoice under a literal "null" string.
    return [...new Set(matching.map((invoice) => invoice.supplierName ?? UNKNOWN_SUPPLIER_NAME))].sort((a, b) =>
      a.localeCompare(b),
    );
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
      const key = invoice.supplierName ?? UNKNOWN_SUPPLIER_NAME;
      const existing = bySupplier.get(key) ?? [];
      existing.push(toListItem(invoice));
      bySupplier.set(key, existing);
    }

    const allGroups: SupplierGroup[] = [...bySupplier.entries()]
      .map(([supplierName, invoices]) => ({
        supplierName,
        count: invoices.length,
        // WP-072: invoiceDate is genuinely nullable - a missing date sorts as if empty.
        invoices: [...invoices].sort((a, b) => (b.invoiceDate ?? '').localeCompare(a.invoiceDate ?? '')),
      }))
      .sort((a, b) => a.supplierName.localeCompare(b.supplierName));

    const totalSuppliers = allGroups.length;
    const start = (params.page - 1) * params.pageSize;
    const groups = allGroups.slice(start, start + params.pageSize);

    return { groups, totalSuppliers, page: params.page, pageSize: params.pageSize };
  }
}

/**
 * Real DTOs for WP-059's `GET /api/invoices/folders|suppliers|grouped`
 * (`FolderSummaryDto`/`SupplierGroupedInvoicesDto`/`SupplierGroupDto` in
 * `SupplierFolderDto.cs`). `FolderSummaryDto`'s fields
 * (`statusCode`/`statusLabel`/`count`) already match `FolderSummary`
 * exactly — no mapping needed there. `SupplierGroupDto.Invoices` reuses
 * the real `InvoiceListItemDto` shape (`supplierInvoiceNumber`/
 * `grossTotal`/`currency`), same field-name conflict `invoiceClient.ts`'s
 * `HttpInvoiceClient` already resolved for `GET /api/invoices` — backend
 * wins, so the same mapping is reused here rather than duplicated.
 */
interface InvoiceListItemResponseDto {
  id: string;
  // WP-077: genuinely nullable per-invoice fields (InvoiceListItemDto.cs) -
  // distinct from SupplierGroupResponseDto.supplierName below, which the
  // backend's SupplierGroupDto.SupplierName never sends null.
  supplierName: string | null;
  supplierInvoiceNumber: string | null;
  invoiceDate: string | null;
  grossTotal: number | null;
  currency: string | null;
  status: string;
  isPotentialDuplicate: boolean;
  duplicateCheckReason: string | null;
  duplicateMatchInvoiceId: string | null;
}

interface SupplierGroupResponseDto {
  supplierName: string;
  count: number;
  invoices: InvoiceListItemResponseDto[];
}

interface SupplierGroupedInvoicesResponseDto {
  groups: SupplierGroupResponseDto[];
  totalSuppliers: number;
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

function mapGroup(dto: SupplierGroupResponseDto): SupplierGroup {
  return {
    supplierName: dto.supplierName,
    count: dto.count,
    invoices: dto.invoices.map(mapListItem),
  };
}

/**
 * Real HTTP-backed implementation, calling WP-059's
 * `GET /api/invoices/folders|suppliers|grouped` (WP-065). `tenantId` is
 * deliberately NOT sent as a query parameter on any of the three calls —
 * same reasoning as `HttpInvoiceClient`: the API resolves the acting
 * tenant from the caller's own Bearer token
 * (`SupplierFolderQueryParameters`'s own doc comment), never from
 * client-supplied input.
 */
export class HttpSupplierFolderClient implements SupplierFolderClient {
  async getFolderCounts(_tenantId: string, search?: string): Promise<FolderSummary[]> {
    return httpClient.get<FolderSummary[]>('/api/invoices/folders', { params: { search } });
  }

  async getSuppliers(_tenantId: string, folder?: string, search?: string): Promise<string[]> {
    return httpClient.get<string[]>('/api/invoices/suppliers', { params: { folder, search } });
  }

  async getGroupedInvoices(params: SupplierFolderQueryParams): Promise<SupplierFolderQueryResult> {
    const response = await httpClient.get<SupplierGroupedInvoicesResponseDto>('/api/invoices/grouped', {
      params: {
        folder: params.folder,
        supplier: params.supplier,
        search: params.search,
        page: params.page,
        pageSize: params.pageSize,
      },
    });

    return {
      groups: response.groups.map(mapGroup),
      totalSuppliers: response.totalSuppliers,
      page: response.page,
      pageSize: response.pageSize,
    };
  }
}

/**
 * The client instance the app uses. `HttpSupplierFolderClient` as of
 * WP-065 — swap this single line back to `new FixtureSupplierFolderClient()`
 * if the real API becomes unreachable during local development.
 */
export const supplierFolderClient: SupplierFolderClient = new HttpSupplierFolderClient();
