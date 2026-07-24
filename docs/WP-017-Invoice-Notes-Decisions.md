# WP-017 — Invoice Notes & Collaboration — Decisions

**Status:** OPEN — implemented with reasoned defaults; needs explicit sign-off.
**Role:** Senior React Engineer
**Depends on:** WP-009 (Invoice Domain Model, incl. `InvoiceNote`), WP-052 Part C
(automatic `NoteAdded` audit logging for `InvoiceService.AddNoteAsync`) — both
implemented on the backend, but no API contract for notes exists yet (see §1).

---

## 1. Data source for now — no backend endpoint exists at all

Same pattern as WP-014/WP-015/WP-016, but one step earlier: for those work
packages, the backend logic existed and only the *API contract* wasn't
confirmed yet. For WP-017, checked `InvoicesController`,
`InvoiceDetailResponse`, and every file under `APFlow.Api` directly —
**no route or DTO for notes exists in any form**, not even an unconfirmed one.
What does exist on the backend: `InvoiceNote` (domain entity),
`IInvoiceService.AddNoteAsync` (validates non-empty/non-whitespace content up
to `FieldLimits.InvoiceNoteContent` = 4000 chars, stages a `NoteAdded` audit
entry), and `IInvoiceRepository.GetByIdWithNotesAsync`. None of this is wired
to `InvoicesController` — `GET /api/invoices/{id}` does not include notes in
`InvoiceDetailResponse`, and there is no `POST` route to call `AddNoteAsync`
through at all.

Per `02_Project_Standards.md` §7 ("never fabricate implementation details");
proceeding required a judgment call between blocking entirely and following
WP-014/015/016's established precedent for exactly this kind of dependency
gap. Chose to follow precedent — this delivery implements:

- `src/types/invoiceNote.ts` — `InvoiceNote` (proposed, additive) and
  `INVOICE_NOTE_CONTENT_MAX_LENGTH` (mirrors `FieldLimits.InvoiceNoteContent`).
- `src/api/invoiceNoteClient.ts` — an `InvoiceNoteClient` interface
  (`getNotes`, `addNote`), with a `FixtureInvoiceNoteClient` implementation
  holding notes in memory, seeded from `src/api/fixtures/invoiceNotes.fixture.ts`.
  Validation in the fixture client mirrors `InvoiceService.AddNoteAsync`'s own
  rules exactly (non-empty/non-whitespace, ≤4000 chars) so the client's error
  path matches what a real API would reject with.
- `src/api/useInvoiceNotes.ts` — the only consumer of the client; swapping in
  a real HTTP-backed implementation is a one-line change
  (`invoiceNoteClient` in `invoiceNoteClient.ts`), same as every other
  fixture client in this codebase.

**Proposed HTTP contract** (for the backend engineer's reference, not binding):

```
GET /api/invoices/{invoiceId}/notes

200 OK
[
  { "id": "string", "content": "string", "authorName": "string", "createdAtUtc": "2026-07-01T14:05:00Z" }
]
```

```
POST /api/invoices/{invoiceId}/notes

Request:
{ "content": "string" }

201 Created
{ "id": "string", "content": "string", "authorName": "string", "createdAtUtc": "2026-07-01T14:05:00Z" }

400 Bad Request — content empty/whitespace-only or over 4000 characters (mirrors AddNoteAsync's existing Result.Failure cases)
404 Not Found — invoiceId does not exist / not visible to the current tenant
```

**Needs confirmation:**
- Exact route, field names/casing, and whether notes are instead folded into
  `InvoiceDetailResponse` (e.g. an added `Notes: IReadOnlyList<InvoiceNoteDto>`
  field) rather than a separate resource — either is straightforward to adapt
  to from the `InvoiceNoteClient` interface.
- `authorName`: `InvoiceNote` only has `AuditEntity.CreatedBy` (a raw
  identifier), not a display name. Whether the API resolves this to a
  friendly name itself, or returns the raw identifier and expects the client
  to resolve it (e.g. against Entra), is a backend/identity decision not made
  here — the fixture client assumes the former (a ready-to-display name) as
  the simpler contract for a consuming UI, per `02_Project_Standards.md` §1.
- Whether a dedicated `POST .../notes` response needs to include anything
  `AddNoteAsync`'s current signature doesn't return today (it returns a bare
  `Result`, not the created note/its id) — the backend method would need a
  small change to return the created `InvoiceNote`'s id and timestamp for a
  controller to shape the `201` response above.

## 2. Chronological order (task 4)

**Decision taken:** oldest-first (ascending by `createdAtUtc`), matching
`AuditSummaryPanel`'s (WP-016) existing convention for its own timestamped
list, for visual/behavioural consistency between the two panels on the same
screen. Sorting happens in `useInvoiceNotes`, not in the display component
(`NotesList`) or the fixture client — `NotesList` renders whatever order it's
given, and `FixtureInvoiceNoteClient.getNotes` deliberately returns notes in
no guaranteed order, so the real API is free to return notes in whatever
order is most efficient for it (e.g. insertion order) without the client's
display logic depending on that assumption.

**Needs confirmation:** whether newest-first (a "latest comment on top" feed
convention) is preferred instead — a one-line change to
`sortChronologically`'s comparator if so.

## 3. Refresh-after-save strategy (task 6)

**Decision taken:** after a successful `addNote`, the full note list is
re-fetched from the client (`getNotes`) rather than optimistically appending
the note returned by `addNote` to local state. Chosen over the optimistic
alternative because refetching is what "refresh notes after save" literally
asks for, and because it exercises the same read path a page reload would use
— so if the real backend's `POST` response ever omits a field the list view
needs, that gap surfaces immediately in this flow rather than being masked by
reusing the `POST` response's own (possibly differently-shaped) object.

## 4. Where the panel lives, and how it fetches

**Decision taken:** `NotesPanel` is a self-contained widget with its own
`useInvoiceNotes(invoiceId)` call, placed on `InvoiceReviewPage` after
`AuditSummaryPanel`. This is a deliberate difference from every other panel on
that page (`InvoiceHeaderSummary`, `ExtractedFieldsPanel`, `AuditSummaryPanel`),
which are purely presentational and receive their data as props from the
page's single `useInvoiceDetail` call.

**Reasoning:** notes have their own independent lifecycle — a list load plus
a write path (add) that immediately needs to re-trigger that same load — which
none of the other panels have. Threading that through `useInvoiceDetail`
would require either extending `InvoiceDetail`/`InvoiceDetailClient` with a
notes-add mutation (mixing a read-only detail fetch with a mutable,
independently-refreshing sub-resource) or lifting note state up into
`InvoiceReviewPage` for a concern the page itself has no other reason to know
about. A self-contained panel keeps `InvoiceReviewPage` unchanged in every
respect except rendering one more panel, per Simplicity First
(`02_Project_Standards.md` §1) and Small, focused components/methods.

## 5. Author identity for newly-added notes

The current acting user's `displayName` (from `useAuth`/`ActingUser` —
WP-014's provisional auth stand-in) is used as `authorName` when a note is
added via the fixture client. This is consistent with how every other
provisional piece of this codebase treats the acting user, and needs no
separate confirmation beyond what WP-014's own stand-in already flags — real
authorship will come from whatever the real `POST` endpoint derives
server-side from the authenticated caller, not from a client-supplied field
(the proposed contract in §1 does not accept `authorName` in the request
body, deliberately).

## 6. Validation (task 7)

Two layers, deliberately duplicating the same rule rather than trusting only
one:

- `AddNoteForm` blocks submission client-side (empty/whitespace-only content,
  content over `INVOICE_NOTE_CONTENT_MAX_LENGTH`), giving immediate feedback
  without a round-trip.
- `FixtureInvoiceNoteClient.addNote` re-validates the same rules and throws if
  they're violated — defense-in-depth per Security Standards §4 ("validate
  all inputs, including internal method inputs where failure would be
  costly"), and it mirrors exactly what `InvoiceService.AddNoteAsync` already
  does server-side, so this client-side duplication is not inventing a new
  rule, only enforcing the one rule that already exists twice on the backend
  (service method + EF Core column configuration).

## 7. No edit/delete affordance

Per WP-017's explicit "Do not allow editing or deleting notes," there is no
edit or delete control anywhere in `NotesList`, `NotesPanel`, or
`useInvoiceNotes` (which exposes only `addNote`, no `updateNote`/`deleteNote`).
Covered directly by a dedicated test in `NotesList.test.tsx`/`NotesPanel.test.tsx`.

---

## AI Agent Rules acknowledgement

Per `02_Project_Standards.md` §7 and `01_Project_Context.md` §9, none of the
above is presented as final. §1 in particular is a larger open item than
WP-014/015/016's own flagged gaps — this is the first frontend WP in this
project's history where the backend has *zero* exposed contract for the
resource being built against, not merely an unconfirmed one — and is flagged
here for Chief Technical Architect / Backend Engineer sign-off before the
fixture client is replaced with a real HTTP-backed one.
