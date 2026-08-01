import { describe, expect, it } from 'vitest';
import { getActingRoleLabel, getRoleDisplayName } from '@/utils/roles';

describe('getRoleDisplayName', () => {
  it('returns the catalogue display name for a known role code', () => {
    expect(getRoleDisplayName('FINANCE_MANAGER')).toBe('Finance Manager / Decision-Maker');
    expect(getRoleDisplayName('AP_REVIEWER')).toBe('AP Reviewer');
  });

  it('falls back to the raw code for an unrecognised role', () => {
    expect(getRoleDisplayName('SOMETHING_ELSE')).toBe('SOMETHING_ELSE');
  });
});

describe('getActingRoleLabel (WP-076)', () => {
  it('returns "Full Approver" for FINANCE_MANAGER', () => {
    expect(getActingRoleLabel(['FINANCE_MANAGER'])).toBe('Full Approver');
  });

  it('returns "Standard Reviewer" for AP_REVIEWER', () => {
    expect(getActingRoleLabel(['AP_REVIEWER'])).toBe('Standard Reviewer');
  });

  it('prioritises Full Approver when a user holds both roles', () => {
    expect(getActingRoleLabel(['AP_REVIEWER', 'FINANCE_MANAGER'])).toBe('Full Approver');
  });

  it('falls back to the platform catalogue display name for any other role', () => {
    expect(getActingRoleLabel(['READ_ONLY'])).toBe('Read-Only');
    expect(getActingRoleLabel(['PLATFORM_ADMIN'])).toBe('Platform Administrator');
  });

  it('returns null for a user with no roles', () => {
    expect(getActingRoleLabel([])).toBeNull();
  });
});
