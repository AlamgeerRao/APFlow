import { describe, expect, it } from 'vitest';
import { decodeAccessTokenRoles } from '@/auth/decodeAccessTokenRoles';

/** Builds a syntactically-real (unsigned) JWT for a given payload - only the payload segment matters here. */
function fakeJwt(payload: unknown): string {
  const base64url = (value: unknown) =>
    btoa(JSON.stringify(value)).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `${base64url({ alg: 'none', typ: 'JWT' })}.${base64url(payload)}.signature`;
}

describe('decodeAccessTokenRoles', () => {
  it('reads the roles claim from a real-shaped access token payload', () => {
    const token = fakeJwt({ aud: 'api://backend', roles: ['FINANCE_MANAGER', 'AP_REVIEWER'] });

    expect(decodeAccessTokenRoles(token)).toEqual(['FINANCE_MANAGER', 'AP_REVIEWER']);
  });

  it('returns an empty array when the claim is absent', () => {
    const token = fakeJwt({ aud: 'api://backend' });

    expect(decodeAccessTokenRoles(token)).toEqual([]);
  });

  it('filters out non-string entries in a malformed roles claim rather than throwing', () => {
    const token = fakeJwt({ roles: ['AP_REVIEWER', 42, null] });

    expect(decodeAccessTokenRoles(token)).toEqual(['AP_REVIEWER']);
  });

  it('returns an empty array for a token with no payload segment, rather than throwing', () => {
    expect(decodeAccessTokenRoles('not-a-real-jwt')).toEqual([]);
  });

  it('returns an empty array for an unparseable payload segment, rather than throwing', () => {
    expect(decodeAccessTokenRoles('header.not-valid-base64url!!!.signature')).toEqual([]);
  });

  it('returns an empty array for an empty string, rather than throwing', () => {
    expect(decodeAccessTokenRoles('')).toEqual([]);
  });
});
