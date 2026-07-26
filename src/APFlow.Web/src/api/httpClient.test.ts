import { describe, expect, it, vi, beforeEach } from 'vitest';

// Mock the token provider so httpClient tests don't depend on real MSAL.
vi.mock('@/auth/tokenProvider', () => ({
  getAccessToken: vi.fn(),
  forceSignIn: vi.fn(),
}));

import { getAccessToken, forceSignIn } from '@/auth/tokenProvider';
import { httpClient, ApiError } from '@/api/httpClient';

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });
}

beforeEach(() => {
  vi.mocked(getAccessToken).mockReset();
  vi.mocked(forceSignIn).mockReset();
  vi.stubGlobal('fetch', vi.fn());
  vi.stubEnv('VITE_API_BASE_URL', 'https://api.test.local');
});

describe('httpClient.get', () => {
  it('attaches a Bearer token from getAccessToken to the request', async () => {
    vi.mocked(getAccessToken).mockResolvedValue('test-token-123');
    vi.mocked(fetch).mockResolvedValue(jsonResponse({ ok: true }));

    await httpClient.get('/api/things');

    const [, init] = vi.mocked(fetch).mock.calls[0];
    expect((init?.headers as Record<string, string>).Authorization).toBe('Bearer test-token-123');
  });

  it('builds the URL with query params, omitting undefined and empty-string values', async () => {
    vi.mocked(getAccessToken).mockResolvedValue('token');
    vi.mocked(fetch).mockResolvedValue(jsonResponse([]));

    await httpClient.get('/api/invoices', { params: { search: 'acme', status: undefined, page: 1, empty: '' } });

    const [url] = vi.mocked(fetch).mock.calls[0];
    const parsed = new URL(url as string);
    expect(parsed.searchParams.get('search')).toBe('acme');
    expect(parsed.searchParams.get('page')).toBe('1');
    expect(parsed.searchParams.has('status')).toBe(false);
    expect(parsed.searchParams.has('empty')).toBe(false);
  });

  it('forces sign-in and throws a 401 ApiError when there is no access token at all', async () => {
    vi.mocked(getAccessToken).mockResolvedValue(null);

    await expect(httpClient.get('/api/invoices')).rejects.toThrow(ApiError);
    expect(forceSignIn).toHaveBeenCalled();
    expect(fetch).not.toHaveBeenCalled();
  });

  it('forces sign-in and throws a 401 ApiError when the server itself returns 401', async () => {
    vi.mocked(getAccessToken).mockResolvedValue('expired-token');
    vi.mocked(fetch).mockResolvedValue(new Response(null, { status: 401 }));

    await expect(httpClient.get('/api/invoices')).rejects.toMatchObject({ status: 401 });
    expect(forceSignIn).toHaveBeenCalled();
  });

  it('maps a ProblemDetails error body to an ApiError with message and code', async () => {
    vi.mocked(getAccessToken).mockResolvedValue('token');
    vi.mocked(fetch).mockResolvedValue(
      jsonResponse({ title: 'Bad request', detail: 'Invalid transition', code: 'Workflow.InvalidToStatus' }, 400),
    );

    await expect(httpClient.get('/api/invoices')).rejects.toMatchObject({
      status: 400,
      message: 'Invalid transition',
      code: 'Workflow.InvalidToStatus',
    });
  });

  it('retries a GET on a 503 up to the retry limit, then succeeds if a later attempt works', async () => {
    vi.mocked(getAccessToken).mockResolvedValue('token');
    vi.mocked(fetch)
      .mockResolvedValueOnce(new Response(null, { status: 503 }))
      .mockResolvedValueOnce(jsonResponse({ ok: true }));

    const result = await httpClient.get('/api/invoices');

    expect(result).toEqual({ ok: true });
    expect(fetch).toHaveBeenCalledTimes(2);
  });

  it('retries a GET on a network error, then throws an ApiError if every attempt fails', async () => {
    vi.mocked(getAccessToken).mockResolvedValue('token');
    vi.mocked(fetch).mockRejectedValue(new TypeError('Failed to fetch'));

    await expect(httpClient.get('/api/invoices')).rejects.toMatchObject({ status: 0 });
    // Initial attempt + MAX_RETRIES(2) = 3 total.
    expect(fetch).toHaveBeenCalledTimes(3);
  });

  it('returns undefined for a 204 No Content response', async () => {
    vi.mocked(getAccessToken).mockResolvedValue('token');
    vi.mocked(fetch).mockResolvedValue(new Response(null, { status: 204 }));

    const result = await httpClient.get('/api/invoices/1/notes');

    expect(result).toBeUndefined();
  });
});

describe('httpClient.post/patch', () => {
  it('never retries a POST, even on a retryable status code', async () => {
    vi.mocked(getAccessToken).mockResolvedValue('token');
    vi.mocked(fetch).mockResolvedValue(new Response(null, { status: 503 }));

    await expect(httpClient.post('/api/invoices/1/notes', { content: 'hi' })).rejects.toMatchObject({ status: 503 });
    expect(fetch).toHaveBeenCalledTimes(1);
  });

  it('sends the body as JSON with a Content-Type header', async () => {
    vi.mocked(getAccessToken).mockResolvedValue('token');
    vi.mocked(fetch).mockResolvedValue(jsonResponse({ id: '1' }));

    await httpClient.patch('/api/invoices/1/status', { targetStatusCode: 'APPROVED', notes: null });

    const [, init] = vi.mocked(fetch).mock.calls[0];
    expect(init?.method).toBe('PATCH');
    expect((init?.headers as Record<string, string>)['Content-Type']).toBe('application/json');
    expect(init?.body).toBe(JSON.stringify({ targetStatusCode: 'APPROVED', notes: null }));
  });
});

describe('httpClient.getBlob', () => {
  it('returns a Blob for a successful binary response', async () => {
    vi.mocked(getAccessToken).mockResolvedValue('token');
    vi.mocked(fetch).mockResolvedValue(new Response(new Blob(['pdf-bytes']), { status: 200 }));

    const blob = await httpClient.getBlob('/api/invoices/1/download');

    expect(blob.size).toBeGreaterThan(0);
    expect(typeof blob.arrayBuffer).toBe('function');
  });

  it('forces sign-in and throws on a 401', async () => {
    vi.mocked(getAccessToken).mockResolvedValue('token');
    vi.mocked(fetch).mockResolvedValue(new Response(null, { status: 401 }));

    await expect(httpClient.getBlob('/api/invoices/1/download')).rejects.toMatchObject({ status: 401 });
    expect(forceSignIn).toHaveBeenCalled();
  });
});
