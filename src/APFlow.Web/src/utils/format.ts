/**
 * Formats an amount using the given ISO 4217 currency code, e.g.
 * formatCurrency(1240.5, 'GBP') -> "£1,240.50".
 *
 * Both `amount` and `currencyCode` are genuinely nullable on the wire
 * (backend `decimal? GrossTotal` / `string? Currency` - Document
 * Intelligence doesn't always extract either from the source PDF) - not
 * just a fixture/test gap. `Intl.NumberFormat` throws a RangeError on a
 * null/empty currency, and `null.toFixed()` throws a TypeError, either of
 * which previously crashed the whole Invoice Queue row (and table) it
 * rendered in, so this renders a fallback instead of throwing - same
 * pattern as `formatDate`.
 */
export function formatCurrency(amount: number | null | undefined, currencyCode: string | null | undefined): string {
  if (amount == null) return '—';
  if (!currencyCode) return `— ${amount.toFixed(2)}`;
  return new Intl.NumberFormat('en-GB', { style: 'currency', currency: currencyCode }).format(amount);
}

/**
 * Formats an ISO 8601 date string for display, e.g. formatDate('2026-07-18') -> "18 Jul 2026".
 *
 * WP-072: `isoDate` is genuinely nullable on the wire (backend `DateOnly? InvoiceDate` -
 * a real invoice can reach this with no extracted date at all, not just a fixture/test
 * gap), so this never throws regardless of what it's given - null/undefined/empty/an
 * unparseable string all render as the fallback rather than crashing the row (and,
 * before this fix, the entire table) that renders it.
 */
export function formatDate(isoDate: string | null | undefined): string {
  if (!isoDate) return '—';

  const date = new Date(`${isoDate}T00:00:00Z`);
  if (Number.isNaN(date.getTime())) return '—';

  return new Intl.DateTimeFormat('en-GB', { day: '2-digit', month: 'short', year: 'numeric', timeZone: 'UTC' }).format(
    date,
  );
}

/**
 * Formats an ISO 8601 timestamp (date + time) for display, e.g.
 * formatDateTime('2026-07-01T08:12:00Z') -> "01 Jul 2026, 08:12". Added for
 * WP-017's Notes panel (task 3: display date/time).
 *
 * WP-092: extended to accept `null | undefined` and to never throw on an
 * unparseable value, same fallback contract as `formatDate` above (both
 * driven by the same real crash pattern: WP-072 found this exact
 * RangeError: Invalid time value in `AuditSummaryPanel`'s own local,
 * now-removed `formatTimestamp`, which had grown its own copy of this same
 * NaN guard rather than sharing it here - consolidated as part of WP-092's
 * "audit every timestamp display and fix inconsistencies" task, now that
 * this component is in scope).
 */
export function formatDateTime(isoTimestamp: string | null | undefined): string {
  if (!isoTimestamp) return '—';

  const date = new Date(isoTimestamp);
  if (Number.isNaN(date.getTime())) return '—';

  return new Intl.DateTimeFormat('en-GB', {
    day: '2-digit',
    month: 'short',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}
