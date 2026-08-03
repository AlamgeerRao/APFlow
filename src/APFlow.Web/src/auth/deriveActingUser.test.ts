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

  it('falls back tenantName to the tenantId, since no standard claim carries a friendly org name', () => {
    const user = deriveActingUser(account({ tenantId: 'tenant-xyz' }));

    expect(user.tenantName).toBe('tenant-xyz');
  });

  it('does not attempt to derive roles - see decodeAccessTokenRoles.ts (WP-081)', () => {
    // The ID token structurally never carries app-role claims (see this
    // module's own doc comment), so deriveActingUser's return type
    // excludes `roles` entirely rather than always returning [] and
    // inviting a caller to trust it.
    const user = deriveActingUser(account({ idTokenClaims: { roles: ['FINANCE_MANAGER'] } }));

    expect(user).not.toHaveProperty('roles');
  });
});
