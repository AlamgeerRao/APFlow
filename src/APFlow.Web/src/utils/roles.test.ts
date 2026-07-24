import { describe, expect, it } from 'vitest';
import { getRoleDisplayName } from '@/utils/roles';

describe('getRoleDisplayName', () => {
  it('returns the catalogue display name for a known role code', () => {
    expect(getRoleDisplayName('FINANCE_MANAGER')).toBe('Finance Manager / Decision-Maker');
    expect(getRoleDisplayName('AP_REVIEWER')).toBe('AP Reviewer');
  });

  it('falls back to the raw code for an unrecognised role', () => {
    expect(getRoleDisplayName('SOMETHING_ELSE')).toBe('SOMETHING_ELSE');
  });
});
