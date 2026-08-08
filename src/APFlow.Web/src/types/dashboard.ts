/** A single recently-created invoice, for the Dashboard's recent activity feed (WP-030). */
export interface RecentActivityItem {
  id: string;
  supplierName: string | null;
  invoiceNumber: string | null;
  status: string;
  /** ISO 8601 timestamp — `Invoice.CreatedAtUtc`, always present (non-nullable on the backend). */
  createdAtUtc: string;
}
