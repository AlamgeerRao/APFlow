import { describe, expect, it } from 'vitest';
import { formatCurrency, formatDate } from '@/utils/format';

describe('formatCurrency', () => {
  it('formats a GBP amount with the currency symbol and two decimal places', () => {
    expect(formatCurrency(1240.5, 'GBP')).toBe('£1,240.50');
  });

  it('formats a whole-number amount with trailing zero decimals', () => {
    expect(formatCurrency(875, 'GBP')).toBe('£875.00');
  });

  it('formats a different currency code with its own symbol', () => {
    expect(formatCurrency(100, 'USD')).toBe('US$100.00');
  });
});

describe('formatDate', () => {
  it('formats an ISO date string as "DD Mon YYYY"', () => {
    expect(formatDate('2026-07-18')).toBe('18 Jul 2026');
  });

  it('formats a date in a different month correctly', () => {
    expect(formatDate('2026-01-05')).toBe('05 Jan 2026');
  });
});
