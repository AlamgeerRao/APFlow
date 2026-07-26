import { describe, expect, it, beforeEach, vi } from 'vitest';
import { getMsalConfig, apiTokenRequest } from '@/auth/msalConfig';

beforeEach(() => {
  vi.unstubAllEnvs();
});

describe('getMsalConfig', () => {
  it('throws a clear error when a required environment variable is missing', () => {
    vi.stubEnv('VITE_ENTRA_CLIENT_ID', '');
    vi.stubEnv('VITE_ENTRA_AUTHORITY', 'https://example.ciamlogin.com/tenant');

    expect(() => getMsalConfig()).toThrow(/VITE_ENTRA_CLIENT_ID/);
  });

  it('builds a valid configuration when all required variables are present', () => {
    vi.stubEnv('VITE_ENTRA_CLIENT_ID', 'client-123');
    vi.stubEnv('VITE_ENTRA_AUTHORITY', 'https://example.ciamlogin.com/tenant');

    const config = getMsalConfig();

    expect(config.auth.clientId).toBe('client-123');
    expect(config.auth.authority).toBe('https://example.ciamlogin.com/tenant');
    expect(config.cache?.cacheLocation).toBe('sessionStorage');
  });

  it('falls back to window.location.origin for redirectUri when unset', () => {
    vi.stubEnv('VITE_ENTRA_CLIENT_ID', 'client-123');
    vi.stubEnv('VITE_ENTRA_AUTHORITY', 'https://example.ciamlogin.com/tenant');
    vi.stubEnv('VITE_ENTRA_REDIRECT_URI', '');

    const config = getMsalConfig();

    expect(config.auth.redirectUri).toBe(window.location.origin);
  });

  it('uses VITE_ENTRA_REDIRECT_URI when explicitly set', () => {
    vi.stubEnv('VITE_ENTRA_CLIENT_ID', 'client-123');
    vi.stubEnv('VITE_ENTRA_AUTHORITY', 'https://example.ciamlogin.com/tenant');
    vi.stubEnv('VITE_ENTRA_REDIRECT_URI', 'https://app.example.com/callback');

    const config = getMsalConfig();

    expect(config.auth.redirectUri).toBe('https://app.example.com/callback');
  });
});

describe('apiTokenRequest', () => {
  it('throws when VITE_API_SCOPE is missing', () => {
    vi.stubEnv('VITE_API_SCOPE', '');

    expect(() => apiTokenRequest()).toThrow(/VITE_API_SCOPE/);
  });

  it('returns the configured scope', () => {
    vi.stubEnv('VITE_API_SCOPE', 'api://backend-client-id/access_as_user');

    expect(apiTokenRequest()).toEqual({ scopes: ['api://backend-client-id/access_as_user'] });
  });
});
