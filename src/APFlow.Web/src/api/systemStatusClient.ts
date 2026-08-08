import type { HealthReport } from '@/types/systemStatus';

/**
 * Client-side contract for WP-043's System Status page: `GET /health/live`
 * and `GET /health/ready` (WP-004/WP-024, no new backend health-check logic —
 * only a JSON `ResponseWriter` added this WP so per-check detail is visible
 * at all, see `ApiServiceCollectionExtensions.BuildHealthCheckResponse`).
 */
export interface SystemStatusClient {
  getLiveness(): Promise<HealthReport>;
  getReadiness(): Promise<HealthReport>;
}

/** Temporary fixture-backed implementation for local development without a reachable API. */
export class FixtureSystemStatusClient implements SystemStatusClient {
  async getLiveness(): Promise<HealthReport> {
    return { status: 'Healthy', checks: [] };
  }

  async getReadiness(): Promise<HealthReport> {
    return {
      status: 'Healthy',
      checks: [
        { name: 'database', status: 'Healthy', description: null },
        { name: 'graph-mailbox', status: 'Healthy', description: null },
        { name: 'blob-storage', status: 'Healthy', description: null },
      ],
    };
  }
}

/**
 * Real implementation, calling the live health endpoints directly rather
 * than through `httpClient` — both are `[AllowAnonymous]` infrastructure
 * endpoints, not part of this API's normal Bearer-authenticated contract,
 * and — the part that actually matters here — `httpClient`'s error handling
 * throws away the response body on any non-2xx status. ASP.NET Core's
 * default health-check status-code mapping returns a real `503` for
 * `Unhealthy` (`Healthy`/`Degraded` both still return `200`), but the JSON
 * body is written by the same `ResponseWriter` regardless of status code —
 * exactly the "which component is degraded/unhealthy" detail this page
 * exists to show, so it can't be allowed to get lost as a generic thrown
 * error on the one status this page most needs to display correctly.
 */
export class HttpSystemStatusClient implements SystemStatusClient {
  private async fetchReport(path: string): Promise<HealthReport> {
    const base = import.meta.env.VITE_API_BASE_URL.replace(/\/$/, '');
    const response = await fetch(`${base}${path}`, { headers: { Accept: 'application/json' } });
    return (await response.json()) as HealthReport;
  }

  getLiveness(): Promise<HealthReport> {
    return this.fetchReport('/health/live');
  }

  getReadiness(): Promise<HealthReport> {
    return this.fetchReport('/health/ready');
  }
}

/**
 * The client instance the app uses. `HttpSystemStatusClient` as the live
 * path — swap this single line back to `new FixtureSystemStatusClient()` if
 * the real API becomes unreachable during local development.
 */
export const systemStatusClient: SystemStatusClient = new HttpSystemStatusClient();
