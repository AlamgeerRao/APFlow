import { describe, expect, it } from 'vitest';
import { formatCurrency, formatDate, formatDateTime } from '@/utils/format';

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

  // A real invoice reached this with a null currencyCode - Document Intelligence
  // extracted no currency symbol for it - and crashed the entire Invoice Queue
  // table, since Intl.NumberFormat() throws RangeError: invalid currency code
  // rather than returning anything.
  it('returns a fallback instead of throwing for null', () => {
    expect(formatCurrency(1240.5, null)).toBe('— 1240.50');
  });

  it('returns a fallback instead of throwing for undefined', () => {
    expect(formatCurrency(1240.5, undefined)).toBe('— 1240.50');
  });

  it('returns a fallback instead of throwing for an empty string', () => {
    expect(formatCurrency(1240.5, '')).toBe('— 1240.50');
  });

  // A real invoice reached this with a null amount too - Document Intelligence
  // extracted no gross total for it - and null.toFixed() throws a TypeError.
  it('returns a fallback instead of throwing for a null amount', () => {
    expect(formatCurrency(null, 'GBP')).toBe('—');
  });

  it('returns a fallback instead of throwing for a null amount and null currency', () => {
    expect(formatCurrency(null, null)).toBe('—');
  });

  it('returns a fallback instead of throwing for an undefined amount', () => {
    expect(formatCurrency(undefined, 'GBP')).toBe('—');
  });
});

describe('formatDate', () => {
  it('formats an ISO date string as "DD Mon YYYY"', () => {
    expect(formatDate('2026-07-18')).toBe('18 Jul 2026');
  });

  it('formats a date in a different month correctly', () => {
    expect(formatDate('2026-01-05')).toBe('05 Jan 2026');
  });

  // WP-072: a real live invoice (Veygo / 2W4WVCTZ-0001) reached this with a null
  // invoiceDate - Document Intelligence extracted no date for it - and crashed the
  // entire Invoice Queue table, since Intl.DateTimeFormat.format() throws
  // RangeError: Invalid time value on an Invalid Date rather than returning anything.
  it('returns a fallback instead of throwing for null', () => {
    expect(formatDate(null)).toBe('—');
  });

  it('returns a fallback instead of throwing for undefined', () => {
    expect(formatDate(undefined)).toBe('—');
  });

  it('returns a fallback instead of throwing for an empty string', () => {
    expect(formatDate('')).toBe('—');
  });

  it('returns a fallback instead of throwing for an unparseable string', () => {
    expect(formatDate('not-a-date')).toBe('—');
  });
});

describe('formatDateTime', () => {
  it('formats an ISO timestamp as "DD Mon YYYY, HH:mm"', () => {
    expect(formatDateTime('2026-07-01T08:12:00Z')).toBe('01 Jul 2026, 08:12');
  });

  it('formats a different timestamp correctly', () => {
    expect(formatDateTime('2026-01-05T23:45:00Z')).toBe('05 Jan 2026, 23:45');
  });
});
