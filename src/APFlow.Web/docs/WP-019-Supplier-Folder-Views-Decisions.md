# WP-019 — Supplier & Folder Views — Decisions

**Status:** OPEN — implemented with reasoned defaults; needs explicit sign-off.
**Role:** Senior React Engineer
**Depends on:** WP-050 (delivered). No real backend endpoint exists yet for a folder-count summary or supplier-grouped invoice listing — same situation WP-015/016/017/018 hit; same fixture-first pattern followed here.

---

## 0. Fixture data re-synced to the current `06_Domain_Reference_Data.md` first

Before starting this WP, `platformDefault.workflowTemplate.ts` and
`gbSkips.workflowTemplate.ts` (WP-014) were found to be stale against the
now-current canonical status catalogue: both still listed the retired
`DUPLICATE_SUSPECTED` status and were missing `EXTRACTED`. One fixture
invoice (`inv-pd-009`) was still seeded with `status: 'DUPLICATE_SUSPECTED'`.

Corrected: both templates now list 13/15 statuses matching
`status-postwb-057.md` §2.6 exactly (`EXTRACTED` added, `DUPLICATE_SUSPECTED`
removed); `inv-pd-009` was reassigned to `AWAITING_REVIEW`, keeping its
`isPotentialDuplicate`/`duplicateCheckReason` flag values unchanged — which
is the correct WP-047 model (duplicate handling is a flag on a normally-
progressing invoice, not a separate status). This wasn't optional for
WP-019 specifically: task 1 builds the folder list directly from this data,
so building on stale data would have produced a folder list that never
matched the live database. Full test suite re-run after this fix; all
128 pre-existing tests still passed before any WP-019 code was added.

## 1. Data source for now

Same pattern as WP-014/015/016/017/018:

- `src/types/supplierFolder.ts` — `FolderSummary`, `SupplierGroup`,
  `SupplierFolderQueryParams`/`Result` (proposed, additive only).
- `src/api/supplierFolderClient.ts` — a `SupplierFolderClient` interface
  with three methods (`getFolderCounts`, `getSuppliers`,
  `getGroupedInvoices`), backed by `FixtureSupplierFolderClient`, built on
  the *same* `invoiceFixtures` array WP-015/018 already use (a WP-018
  status change is immediately visible here too) and the *same*
  `workflowTemplateClient` fixtures WP-014 uses for the folder list — no
  separate, possibly-divergent fixture data invented for this WP.
- `matchesInvoiceSearch` was extracted from WP-015's `invoiceClient.ts`
  (previously private, now exported) and reused here rather than
  duplicated, so both clients' search semantics can't silently drift apart.

**Proposed HTTP contract** (for the backend engineer's reference, not binding):

```
GET /api/invoices/folders?search=
200 OK: [ { "statusCode": "string", "statusLabel": "string", "count": number } ]

GET /api/invoices/suppliers?folder=&search=
200 OK: ["string", ...]

GET /api/invoices/grouped?folder=&supplier=&search=&page=&pageSize=
200 OK: {
  "groups": [ { "supplierName": "string", "count": number, "invoices": [ ...InvoiceListItemDto ] } ],
  "totalSuppliers": number, "page": number, "pageSize": number
}
```

**Needs confirmation:** exact routes/field names; whether folder counts and
supplier options should be separate round-trips (as proposed) or folded
into one combined response — proposed separately here because each has a
different, independent set of dependencies (folder counts don't depend on
the supplier filter; supplier options don't depend on the page), but a
combined endpoint is a reasonable alternative.

## 2. "Remember selected filters" — URL query string, not localStorage

**Decision taken:** folder, supplier, search, and page are synced to the
URL (`?folder=...&supplier=...&search=...&page=...`) via
`useSearchParams`, not to `localStorage`/`sessionStorage`.

**Reasoning:** this makes the browser's own back/forward buttons and a
page refresh preserve the filters "for free," and a filtered view becomes
a shareable/bookmarkable link — a common, low-complexity pattern for list
views, requiring no new dependency. The alternative (persisting to
`localStorage` so filters survive even after closing the tab and
navigating back to the app later, independent of URL) is a materially
different UX decision, not attempted here without confirmation.

**Needs confirmation:** whether "remember" should mean cross-session
persistence (survives closing the tab) rather than just cross-navigation
persistence (survives back/forward and refresh, which is what's built).
If cross-session is wanted, adding a `localStorage` fallback that seeds the
URL on first load is a small, additive change to `useSupplierFolderView`
and would not require touching any component.

## 3. Pagination unit: supplier groups, not individual invoices

Task 5 ("Support pagination") is ambiguous between paginating the flat
invoice list or the grouped-by-supplier list. **Decision taken:** paginate
supplier groups (default page size 5), since task 2's grouping is the
page's core structural unit — each group is shown in full (not truncated)
via the existing `InvoiceQueueTable`, so a supplier with many invoices
doesn't get artificially cut off mid-list.

**Needs confirmation:** whether a very high-volume supplier (many invoices
in one group) should itself be paginated/truncated with a "show more" —
not needed for any current fixture supplier, but worth flagging as a scale
consideration.

## 4. Reused `InvoiceQueueTable` per supplier group — sorting is a no-op here

Each supplier group renders invoices via WP-015's existing
`InvoiceQueueTable` rather than a second table implementation, so duplicate
highlighting, status badges, and click-to-review navigation are inherited
for free and stay consistent with the Invoice Queue (DRY, per
`02_Project_Standards.md` §1). Its sortable column headers are still
visually present and clickable, but `onSortChange` is a no-op here —
WP-019's task list didn't ask for per-group sorting, and rows are
pre-sorted by date (newest first) by the client. This is a known,
accepted trade-off of reusing the component as-is rather than forking it
or adding a "sortable" prop; flagged in case per-group sorting turns out
to be wanted.

## 5. Search and supplier filter behave independently, not as a strict hierarchy

Selecting a supplier via the filter dropdown does not require a folder to
already be selected, and vice versa — folder, supplier, and search all
narrow the same underlying query independently and can be combined freely
(e.g. "GB Skips, folder=Awaiting Review, supplier=Dales Aggregates,
search=DAG"). This is the simplest, least surprising interaction model and
matches how WP-015's search/status filter already behave together.

---

## AI Agent Rules acknowledgement

Per `02_Project_Standards.md` §7 and `01_Project_Context.md` §9, none of
the above is presented as final. §1, §2, §3, and §4 are implemented with
reasoned defaults and flagged here for Chief Technical Architect / backend
sign-off, consistent with WP-014–018's precedent. §0's fixture correction
is not an open decision — it re-syncs existing fixtures to already-approved
reference data and needs no further sign-off.
