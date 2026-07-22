# WP-015 — Invoice Work Queue — Decisions

**Status:** OPEN — implemented with reasoned defaults; needs explicit sign-off.
**Role:** Senior React Engineer
**Depends on:** WP-011 (Invoice Repository & Query Services) — implemented on the backend, but its report/API contract was not available when WP-015 was built.

---

## 1. Data source for now

Same pattern as WP-014/WP-050: since WP-011's real endpoint contract wasn't available, this delivery implements:

- `src/types/invoice.ts` — `InvoiceListItem`/`InvoiceQueryParams`/`InvoiceQueryResult` client-side types (proposed, additive only).
- `src/api/invoiceClient.ts` — an `InvoiceClient` interface (`queryInvoices(params)`), with a `FixtureInvoiceClient` implementation performing real search/filter/sort/pagination against local fixture data (`src/api/fixtures/invoices.fixture.ts`) spanning both tenants, multiple statuses, and flagged duplicates.
- The only confirmed field names used are `isPotentialDuplicate`/`duplicateCheckReason` (from the WP-012 report); everything else (`supplierName`, `invoiceNumber`, `invoiceDate`, `amount`, `currencyCode`, `status`, `id`) is a reasoned, additive proposal.
- All consumers (`useInvoiceQueue`) depend only on the `InvoiceClient` interface — swapping in a real HTTP client is a one-line change (`invoiceClient` in `invoiceClient.ts`).

**Proposed HTTP contract for WP-011** (for the backend engineer's reference, not binding):

```
GET /api/invoices?search=&status=&sortBy=&sortDirection=&page=&pageSize=

200 OK
{
  "items": [
    {
      "id": "string", "supplierName": "string", "invoiceNumber": "string",
      "invoiceDate": "2026-07-18", "amount": 1240.50, "currencyCode": "GBP",
      "status": "string", "isPotentialDuplicate": boolean, "duplicateCheckReason": "string|null"
    }
  ],
  "totalCount": number, "page": number, "pageSize": number
}
```

**Needs confirmation:** exact endpoint route, field names/casing, param names, and default/max page size.

## 2. Scope of the default (unfiltered) view

WP-015's objective line reads "Display invoices awaiting review," but the task list also asks for a Status *column* and a Status *filter* — which only make sense if more than one status can appear at once. **Decision taken:** the unfiltered `/invoices` view shows every non-terminal status for the tenant (i.e. the general "in-flight" work queue), and the Status filter narrows to one specific status. Navigating from WP-014's per-status nav sub-links (`/invoices/:statusCode`) pre-applies that status as the initial filter, but the user can still clear it back to "All statuses."

**Reasoning:** showing a Status column only makes sense if rows can differ; restricting the base view to literally just `AWAITING_REVIEW` would make the Status column and Status filter mostly redundant. This interpretation keeps both controls meaningful without inventing a business rule about which specific statuses "count" as the review queue.

**Needs confirmation:** whether the unfiltered view should instead default to a narrower status set (e.g. only `AWAITING_REVIEW` + GB Skips' two extra review states) rather than every non-terminal status.

## 3. Search scope

Search matches supplier name and invoice number (case-insensitive substring), since those are the two identifying text fields on the row. Not extended to amount or date, since a text search over numeric/date fields wasn't asked for and risks unpredictable partial matches (e.g. "1" matching every amount containing a 1).

## 4. Sorting and pagination

- Sortable columns: all five requested display columns (Supplier, Invoice Number, Date, Amount, Status) via clickable header buttons, toggling direction on repeated clicks.
- Pagination: Previous/Next with a fixed page size of 10 and a "Showing X–Y of Z" summary, rather than a page-size selector or numbered page list — the task asked for "Pagination" without specifying a page size or control style; this is the simplest option that satisfies it (per `02_Project_Standards.md` §1, Simplicity First).

**Needs confirmation:** desired page size, and whether a page-size selector or numbered pages are wanted instead of Previous/Next.

## 5. Duplicate highlighting

Rows where `isPotentialDuplicate` is true get a light amber row background plus a "Possible duplicate" badge; the badge's `title` attribute and a screen-reader-only span surface `duplicateCheckReason` (from WP-010/WP-012) without requiring a click, since this page is read-only and has no row-level detail view to show it in otherwise.

## 6. Read-only scope maintained

No row is clickable, no action buttons are rendered, and no approve/edit/note affordance exists anywhere in this delivery, consistent with WP-015's explicit Out of Scope list.

---

## AI Agent Rules acknowledgement

Per `02_Project_Standards.md` §7 and `01_Project_Context.md` §9, none of the above is presented as final. Items in §1 and §2 in particular are implemented with reasoned defaults and flagged here for Chief Technical Architect / backend sign-off, consistent with WP-014's precedent for handling a dependency whose contract wasn't yet available.
