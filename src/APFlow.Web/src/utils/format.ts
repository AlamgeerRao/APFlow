/** Formats an amount using the given ISO 4217 currency code, e.g. formatCurrency(1240.5, 'GBP') -> "£1,240.50". */
export function formatCurrency(amount: number, currencyCode: string): string {
  return new Intl.NumberFormat('en-GB', { style: 'currency', currency: currencyCode }).format(amount);
}

/** Formats an ISO 8601 date string for display, e.g. formatDate('2026-07-18') -> "18 Jul 2026". */
export function formatDate(isoDate: string): string {
  const date = new Date(`${isoDate}T00:00:00Z`);
  return new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC' }).format(
    date,
  );
}
