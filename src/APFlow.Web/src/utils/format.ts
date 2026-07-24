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

/**
 * Formats an ISO 8601 timestamp (date + time) for display, e.g.
 * formatDateTime('2026-07-01T08:12:00Z') -> "01 Jul 2026, 08:12". Added for
 * WP-017's Notes panel (task 3: display date/time). Deliberately not used
 * to refactor `AuditSummaryPanel`'s existing local `formatTimestamp` — that
 * component is out of scope for this work package (Development Workflow §9:
 * "do not change unrelated files").
 */
export function formatDateTime(isoTimestamp: string): string {
  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    timeZone: 'UTC',
  }).format(new Date(isoTimestamp));
}
