import { getAccessToken, forceSignIn } from '@/auth/tokenProvider';

/**
 * Central API client (WP-020 tasks 2–3). Built on native `fetch` rather
 * than adding Axios — the task itself says "Axios or equivalent," and
 * `fetch` needs zero new dependencies, consistent with
 * `02_Project_Standards.md` §1–2 (Simplicity First, prefer built-in
 * capabilities, minimise dependencies). See
 * docs/WP-020-Real-Auth-And-Api-Integration-Decisions.md §2 for the full
 * reasoning.
 *
 * Every request attaches a real Bearer token (task 2). Global behaviour
 * (task 3): 401 responses trigger a forced re-sign-in rather than
 * surfacing a bare auth error; GET requests retry transient failures
 * (network errors and 502/503/504) with backoff — mutating requests
 * (POST/PATCH) never auto-retry, to avoid double-submitting an action
 * WP-054's endpoints have side effects for.
 */

export class ApiError extends Error {
  readonly status: number;
  /** Machine-readable failure code from a ProblemDetails body's `code` field (see WP-054's contract), when present. */
  readonly code?: string;

  constructor(status: number, message: string, code?: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

interface RequestOptions {
  params?: Record<string, string | number | string[] | undefined>;
  signal?: AbortSignal;
}

const RETRYABLE_STATUSES = new Set([502, 503, 504]);
const MAX_RETRIES = 2;
const RETRY_BASE_DELAY_MS = 300;

/**
 * An array value (WP-074, e.g. `statuses`) is appended as repeated query params
 * (`?statuses=A&statuses=B`) - ASP.NET Core's standard binding convention for a
 * `[FromQuery] string[]` action parameter - rather than a single comma-joined
 * value, which it does not parse into an array by default.
 */
function buildUrl(path: string, params?: RequestOptions['params']): string {
  const base = import.meta.env.VITE_API_BASE_URL.replace(/\/$/, '');
  const url = new URL(base + path);
  if (params) {
    for (const [key, value] of Object.entries(params)) {
      if (value === undefined || value === '') continue;
      if (Array.isArray(value)) {
        for (const item of value) {
          url.searchParams.append(key, item);
        }
      } else {
        url.searchParams.set(key, String(value));
      }
    }
  }
  return url.toString();
}

/** Extracts a message/code from a WP-054-style ProblemDetails error body, falling back gracefully if the body isn't JSON. */
async function parseErrorBody(response: Response): Promise<{ message: string; code?: string }> {
  try {
    const body = (await response.json()) as { detail?: string; title?: string; code?: string };
    return { message: body.detail ?? body.title ?? response.statusText, code: body.code };
  } catch {
    return { message: response.statusText || `Request failed with status ${response.status}` };
  }
}

function delay(ms: number): Promise<void> {
  return new Promise((resolve) => {
    setTimeout(resolve, ms);
  });
}

async function request<T>(
  method: 'GET' | 'POST' | 'PATCH' | 'PUT',
  path: string,
  options: RequestOptions & { body?: unknown } = {},
): Promise<T> {
  const url = buildUrl(path, options.params);
  const isRetryable = method === 'GET';
  let attempt = 0;

  for (;;) {
    const token = await getAccessToken();
    if (!token) {
      await forceSignIn();
      throw new ApiError(401, 'You need to sign in to continue.');
    }

    const headers: Record<string, string> = {
      Accept: 'application/json',
      Authorization: `Bearer ${token}`,
    };
    if (options.body !== undefined) {
      headers['Content-Type'] = 'application/json';
    }

    let response: Response;
    try {
      response = await fetch(url, {
        method,
        headers,
        body: options.body !== undefined ? JSON.stringify(options.body) : undefined,
        signal: options.signal,
      });
    } catch {
      if (isRetryable && attempt < MAX_RETRIES) {
        attempt += 1;
        await delay(RETRY_BASE_DELAY_MS * attempt);
        continue;
      }
      throw new ApiError(0, 'Network error — please check your connection and try again.');
    }

    if (response.status === 401) {
      await forceSignIn();
      throw new ApiError(401, 'Your session has expired. Please sign in again.');
    }

    if (!response.ok) {
      if (isRetryable && RETRYABLE_STATUSES.has(response.status) && attempt < MAX_RETRIES) {
        attempt += 1;
        await delay(RETRY_BASE_DELAY_MS * attempt);
        continue;
      }
      const { message, code } = await parseErrorBody(response);
      throw new ApiError(response.status, message, code);
    }

    if (response.status === 204) {
      return undefined as T;
    }
    return (await response.json()) as T;
  }
}

/** Fetches a binary resource (e.g. a PDF) with the same auth/retry/error handling, returning a Blob rather than parsed JSON. */
async function getBlob(path: string, options: RequestOptions = {}): Promise<Blob> {
  const url = buildUrl(path, options.params);
  const token = await getAccessToken();
  if (!token) {
    await forceSignIn();
    throw new ApiError(401, 'You need to sign in to continue.');
  }

  const response = await fetch(url, {
    headers: { Authorization: `Bearer ${token}` },
    signal: options.signal,
  });

  if (response.status === 401) {
    await forceSignIn();
    throw new ApiError(401, 'Your session has expired. Please sign in again.');
  }
  if (!response.ok) {
    const { message, code } = await parseErrorBody(response);
    throw new ApiError(response.status, message, code);
  }
  return response.blob();
}

export const httpClient = {
  get: <T>(path: string, options?: RequestOptions) => request<T>('GET', path, options),
  post: <T>(path: string, body?: unknown, options?: RequestOptions) => request<T>('POST', path, { ...options, body }),
  patch: <T>(path: string, body?: unknown, options?: RequestOptions) =>
    request<T>('PATCH', path, { ...options, body }),
  /** WP-027: full-field-replace update (e.g. `PUT /api/suppliers/{id}`) — never auto-retried, same reasoning as POST/PATCH. */
  put: <T>(path: string, body?: unknown, options?: RequestOptions) => request<T>('PUT', path, { ...options, body }),
  getBlob,
};
