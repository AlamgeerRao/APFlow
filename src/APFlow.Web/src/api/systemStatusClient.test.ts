import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest';
import { FixtureSystemStatusClient, HttpSystemStatusClient } from '@/api/systemStatusClient';

describe('FixtureSystemStatusClient', () => {
  const client = new FixtureSystemStatusClient();

  it('getLiveness returns a Healthy report with no checks', async () => {
    const report = await client.getLiveness();
    expect(report).toEqual({ status: 'Healthy', checks: [] });
  });

  it('getReadiness returns a Healthy report with the three real dependency checks', async () => {
    const report = await client.getReadiness();
    expect(report.status).toBe('Healthy');
    expect(report.checks.map((c) => c.name)).toEqual(['database', 'graph-mailbox', 'blob-storage']);
  });
});

describe('HttpSystemStatusClient', () => {
  const originalFetch = global.fetch;

  beforeEach(() => {
    global.fetch = vi.fn();
    // Unlike every other client test in this codebase, this one exercises the
    // real fetchReport implementation (not a mocked httpClient.get), so it's
    // the first to actually read import.meta.env.VITE_API_BASE_URL - unset in
    // the shared vite.config.ts test env (only TZ is), so it's stubbed here,
    // scoped to this file only.
    vi.stubEnv('VITE_API_BASE_URL', 'https://api.test.local');
  });

  afterEach(() => {
    global.fetch = originalFetch;
    vi.unstubAllEnvs();
  });

  it('getLiveness calls GET /health/live and returns the parsed body', async () => {
    vi.mocked(global.fetch).mockResolvedValueOnce({
      json: async () => ({ status: 'Healthy', checks: [] }),
    } as Response);

    const client = new HttpSystemStatusClient();
    const report = await client.getLiveness();

    expect(global.fetch).toHaveBeenCalledWith(
      expect.stringContaining('/health/live'),
      expect.objectContaining({ headers: { Accept: 'application/json' } }),
    );
    expect(report).toEqual({ status: 'Healthy', checks: [] });
  });

  it('getReadiness still returns the parsed body on a 503 (Unhealthy) response, not a thrown error', async () => {
    // ASP.NET Core's default health-check status-code mapping returns 503 for
    // Unhealthy - the JSON body is still written by the same ResponseWriter
    // regardless, and this is exactly the case the page most needs to show
    // correctly, so it must not be lost as a generic thrown error.
    vi.mocked(global.fetch).mockResolvedValueOnce({
      status: 503,
      ok: false,
      json: async () => ({
        status: 'Unhealthy',
        checks: [{ name: 'database', status: 'Unhealthy', description: 'Could not connect.' }],
      }),
    } as Response);

    const client = new HttpSystemStatusClient();
    const report = await client.getReadiness();

    expect(report.status).toBe('Unhealthy');
    expect(report.checks[0]).toEqual({ name: 'database', status: 'Unhealthy', description: 'Could not connect.' });
  });
});
