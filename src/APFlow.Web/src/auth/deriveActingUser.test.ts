import { describe, expect, it } from 'vitest';
import type { AccountInfo } from '@azure/msal-browser';
import { deriveActingUser } from '@/auth/deriveActingUser';

function account(overrides: Partial<AccountInfo> = {}): AccountInfo {
  return {
    homeAccountId: 'home-1',
    environment: 'login.microsoftonline.com',
    tenantId: 'tenant-abc-123',
    username: 'jamie@example.com',
    localAccountId: 'local-1',
    name: 'Jamie Lee',
    idTokenClaims: {},
    ...overrides,
  } as AccountInfo;
}

describe('deriveActingUser', () => {
  it('maps tenantId from the MSAL account tenantId (parsed tid claim)', () => {
    const user = deriveActingUser(account({ tenantId: 'gb-skips-tenant-id' }));

    expect(user.tenantId).toBe('gb-skips-tenant-id');
  });

  it('uses account.name as displayName when present', () => {
    const user = deriveActingUser(account({ name: 'Patrick Skips' }));

    expect(user.displayName).toBe('Patrick Skips');
  });

  it('falls back to the preferred_username claim when account.name is absent', () => {
    const user = deriveActingUser(
      account({ name: '', idTokenClaims: { preferred_username: 'patrick@gbskips.example' } }),
    );

    expect(user.displayName).toBe('patrick@gbskips.example');
  });

  it('falls back to account.username when neither name nor preferred_username is present', () => {
    const user = deriveActingUser(account({ name: '', username: 'fallback@example.com', idTokenClaims: {} }));

    expect(user.displayName).toBe('fallback@example.com');
  });

  it('maps the roles claim when present', () => {
    const user = deriveActingUser(account({ idTokenClaims: { roles: ['FINANCE_MANAGER', 'AP_REVIEWER'] } }));

    expect(user.roles).toEqual(['FINANCE_MANAGER', 'AP_REVIEWER']);
  });

  it('defaults to an empty roles array when the claim is absent', () => {
    const user = deriveActingUser(account({ idTokenClaims: {} }));

    expect(user.roles).toEqual([]);
  });

  it('filters out non-string entries in a malformed roles claim rather than throwing', () => {
    const malformedAccount = {
      ...account(),
      idTokenClaims: { roles: ['AP_REVIEWER', 42, null] },
    } as unknown as AccountInfo;

    const user = deriveActingUser(malformedAccount);

    expect(user.roles).toEqual(['AP_REVIEWER']);
  });

  it('falls back tenantName to the tenantId, since no standard claim carries a friendly org name', () => {
    const user = deriveActingUser(account({ tenantId: 'tenant-xyz' }));

    expect(user.tenantName).toBe('tenant-xyz');
  });
});
