import type { RecentActivityItem } from '@/types/dashboard';
import { invoiceFixtures } from '@/api/fixtures/invoices.fixture';
import { httpClient } from '@/api/httpClient';

/**
 * Client-side contract for the Dashboard's (WP-030) recent-activity feed.
 * Status counts reuse `supplierFolderClient.getFolderCounts` directly
 * (WP-059) — no separate client method needed for that part, per
 * `docs/Sprint2-Plan.md` §3 WP-030's own confirmation.
 */
export interface DashboardClient {
  /** The `limit` most recently created invoices for the acting tenant, newest first. */
  getRecentActivity(tenantId: string, limit: number): Promise<RecentActivityItem[]>;
}

/**
 * Temporary fixture-backed implementation, built on the same
 * `invoiceFixtures` array every other fixture client in this codebase uses.
 * `invoiceFixtures` carries no `createdAtUtc` field (it predates this WP and
 * nothing else needed one) — `invoiceDate` is used as this fixture's own
 * recency proxy instead, since real ingestion order isn't otherwise
 * reconstructable from the existing fixture shape. This client is dev-only
 * fallback, never the live path (see `dashboardClient` below).
 */
export class FixtureDashboardClient implements DashboardClient {
  async getRecentActivity(tenantId: string, limit: number): Promise<RecentActivityItem[]> {
    const matching = invoiceFixtures.filter((invoice) => invoice.tenantId === tenantId);

    return [...matching]
      .sort((a, b) => (b.invoiceDate ?? '').localeCompare(a.invoiceDate ?? ''))
      .slice(0, limit)
      .map((invoice) => ({
        id: invoice.id,
        supplierName: invoice.supplierName,
        invoiceNumber: invoice.invoiceNumber,
        status: invoice.status,
        createdAtUtc: invoice.invoiceDate ? `${invoice.invoiceDate}T00:00:00Z` : new Date(0).toISOString(),
      }));
  }
}

/**
 * Real DTO for `GET /api/invoices` (WP-058) — reuses the same, already-live
 * endpoint the Invoice Queue calls, sorted by `CreatedAtUtc` descending (the
 * backend's own default sort field, `InvoiceQueryParameters.cs`) rather than
 * a new endpoint, per WP-030's confirmed scope. Deliberately a narrower DTO
 * than `invoiceClient.ts`'s own `InvoiceListItemResponseDto` — this feed only
 * ever renders id/supplier/invoice-number/status/createdAtUtc, so only those
 * fields are declared here.
 */
interface RecentActivityResponseDto {
  id: string;
  supplierName: string | null;
  supplierInvoiceNumber: string | null;
  status: string;
  createdAtUtc: string;
}

interface InvoiceQueryResponseDto {
  items: RecentActivityResponseDto[];
}

export class HttpDashboardClient implements DashboardClient {
  async getRecentActivity(_tenantId: string, limit: number): Promise<RecentActivityItem[]> {
    const response = await httpClient.get<InvoiceQueryResponseDto>('/api/invoices', {
      params: {
        sortBy: 'CreatedAtUtc',
        sortDescending: 'true',
        page: 1,
        pageSize: limit,
      },
    });

    return response.items.map((dto) => ({
      id: dto.id,
      supplierName: dto.supplierName,
      invoiceNumber: dto.supplierInvoiceNumber,
      status: dto.status,
      createdAtUtc: dto.createdAtUtc,
    }));
  }
}

/**
 * The client instance the app uses. `HttpDashboardClient` as the live path —
 * swap this single line back to `new FixtureDashboardClient()` if the real
 * API becomes unreachable during local development.
 */
export const dashboardClient: DashboardClient = new HttpDashboardClient();
