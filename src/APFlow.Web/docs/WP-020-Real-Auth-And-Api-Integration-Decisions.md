# WP-020 — Real Authentication & API Integration — Decisions

**Status:** OPEN — implemented with reasoned defaults; live verification blocked, needs explicit sign-off.
**Role:** Senior React Engineer
**Dependencies:** WP-058 (delivered), a real/dev-tier Entra External ID tenant (not available in this environment — see §0).

---

## 0. What could not be done, and why

Two of this WP's stated dependencies were not available in the environment this WP was built in: a real/dev-tier Entra External ID tenant (explicitly a DevOps dependency in the WP's own text) and a reachable running `APFlow.Api` instance with a real database. Per explicit confirmation before starting: build fully env-var-configurable, defer live verification. Concretely, this means:

- Every claim shape (`tenantId`, `roles`, `displayName` — see `deriveActingUser.ts`) is a reasoned assumption, not verified against a real issued token. This mirrors `docs/WP-002-Entra-Verification-Checklist.md`'s own caveat from the backend side — the frontend hits the identical gap.
- Every real backend response shape (`InvoiceQueryResult`, `InvoiceDetailResponseDto`, `InvoiceNoteDto`) is built from status-postwb-057.md's prose description of the live API surface, not a captured real response.
- The deliverable's own "confirmation each swapped screen works against the real API in a local/dev environment" could not be produced by me. `docs/WP-020-Manual-Verification-Checklist.md` is the closest substitute: a concrete checklist for whoever does have real environment access, cross-referencing exactly which assumptions in this delivery need checking and where to fix them if wrong.
- Everything that *could* be verified without live infrastructure was: 177 unit tests, including 26 new ones for this WP specifically, mock `httpClient`/MSAL directly and exercise the actual request-building, response-mapping, retry, and error-handling logic end-to-end at the code level.

## 1. Claim mapping (`deriveActingUser.ts`)

- `tenantId` ← MSAL's own parsed `account.tenantId` (from the `tid` claim) — this is AP Flow's own multi-tenant key, matching the backend's `WorkflowTemplate.TenantId`.
- `displayName` ← `account.name`, falling back to `preferred_username`, falling back to `account.username`.
- `tenantName` ← falls back to the tenant ID itself. No standard Entra ID token claim carries a friendly organisation display name; a real one would need either a custom claim on the app registration or a separate API lookup, neither implemented here.
- `roles` ← the standard Entra App Roles claim (`roles`), assuming the backend's app registration issues app roles matching `06_Domain_Reference_Data.md` §1's catalogue exactly.

**Needs confirmation:** all four, against a real issued token — see the checklist §1.

## 2. Central API client: `fetch`, not Axios

The task said "Axios or equivalent." Built on native `fetch` — zero new dependencies, consistent with `02_Project_Standards.md` §1–2 (Simplicity First, prefer built-in capabilities, minimise dependencies). MSAL itself (`@azure/msal-browser`/`@azure/msal-react`) was added since task 1 explicitly named it.

Retry policy: GET requests retry network errors and 502/503/504 up to twice with linear backoff; POST/PATCH never auto-retry, to avoid double-submitting a mutating action. Session-expiry: any 401, or no token being acquirable at all, forces a fresh interactive sign-in (`forceSignIn`) rather than surfacing a bare auth error to the caller.

**Needs confirmation:** the retry count/backoff and the choice not to retry mutations are reasoned defaults, not specified numbers from any WP.

## 3. Real client swaps — per-WP notes

All four (WP-015/016/017/018) now call real endpoints. **Response DTO field names are now confirmed correct — see §7.** Two things discovered mid-swap, not anticipated by the WP text as written:

- **PDF authentication gap.** `GET /api/invoices/{id}/download` requires the same Bearer token every endpoint does, but the native `<object>`/`<a>` elements `InvoicePdfViewer` uses (Architect-approved Option A, `docs/WP-016-Invoice-Review-Decisions.md` §1) can't attach an Authorization header to a plain URL load. Resolved inside `HttpInvoiceDetailClient`: fetch the PDF as an authenticated `Blob`, hand the viewer a `blob:` object URL instead. No component changed — both `InvoicePdfViewer` and `InvoiceReviewPage` already just treated `pdfUrl` as "a URL the browser can load," which a blob URL satisfies identically. `useInvoiceDetail.ts` revokes the previous object URL on every new load and on unmount, to avoid leaking one per Prev/Next navigation.
- **`available-actions` needs the invoice ID, not just the status.** WP-018's original client interface only took `fromStatusCode` (sufficient for the fixture, which could answer purely from status+role). WP-054's real endpoint is `GET /api/invoices/{id}/available-actions` — per-invoice. Fixed by adding `invoiceId` to `getAvailableActions`'s signature; `useWorkflowActions` already had it in scope, so this was a small, contained change.

`ExtractedField.confidenceScore` was widened from `number` to `number | null` — the status report confirms `Currency` always has a null score (WP-056 ruling); the type was wrong, now fixed, with `ConfidenceBadge` updated to render "Not available" for null. `InvoiceDetail.overallConfidenceScore` is now explicitly documented as **client-computed** (mean of non-null field scores) rather than a real server field — nothing in the documented live API surface suggests the server returns an aggregate.

`executeAction` now returns the full updated `InvoiceDetail` (minus `pdfUrl`, which the caller merges from the invoice already on screen — the document never changes on a status transition, so re-fetching it would only leak another object URL), per WP-054's real contract and the Chief Technical Architect's explicit instruction (`docs/Backlog.md`) to use it directly rather than a second `retry()`. `useInvoiceDetail.ts` gained `applyUpdatedInvoice` for this; `WorkflowActionsPanel`'s `onStatusChanged` now passes the updated invoice through instead of taking no arguments.

`invoiceNoteClient`'s `addNote` no longer sends a client-supplied author name in the real implementation (accepted for interface/call-site compatibility, silently ignored) — the server resolves it authoritatively from `ICurrentUserService.DisplayName` per WP-055's ruling. Trusting a client-supplied name for an audit-relevant field would have been a spoofing risk the fixture client never had to consider.

## 4. WP-019 stays on fixtures — new backlog item, not silently skipped

Task 5 asked to swap WP-015/016/017/018/**019**'s fixture clients. WP-019's wasn't swapped: no backend endpoint exists anywhere for folder-count summaries, supplier-name listings, or supplier-grouped invoice listings — status-postwb-057.md §2.2's live API surface table has no such routes. Swapping it would mean inventing the endpoints myself, which is exactly the business/API-contract invention `02_Project_Standards.md` §7 prohibits. `supplierFolderClient.ts`'s own doc comment now states this explicitly. **New backlog item, for whoever picks up WP-019's real integration**: a backend WP needs to build (or confirm doesn't exist and won't) the three endpoints `docs/WP-019-Supplier-Folder-Views-Decisions.md` §1 already proposed, before this specific swap can happen.

## 5. Task 6 (reconcile WP-018's fixture against WP-053's full 57-row graph) — partially blocked

Completed: the fixture file you pasted (already correctly reflecting the 2026-07-25 Architect correction — all four real role-gated edges, plus the previously-known GB-specific proposed set) was used as-is; its `getAvailableActions`/`executeAction` signatures were updated for the real-client swap (§3 above), and its tests updated to match.

**Not completed, and not attempted by guessing:** the fixture's remaining edges — the platform-default template's ~23 undocumented transitions and GB Skips' further edges beyond what's already in the fixture — were not filled in, because I don't have WP-053's full 57-row transition table anywhere in what's been shared with me (only fragments: the 4 role-gated edges, the previously-known GB-specific proposed set, and the two templates' total status counts). Since the fixture client is no longer the live path for this WP (the real HTTP client is), and since `02_Project_Standards.md` §7 is explicit ("never guess at business logic... never fabricate implementation details not provided"), inventing 20+ transition rows to fill this gap was not attempted. **If WP-053's full transition table (or its decision doc) can be shared, I can complete this reconciliation as a follow-up in one pass** — it's a data-entry task at that point, not a design one.

## 6. What's unchanged

WP-014's `ActingUser`/`AuthContextValue` shape was deliberately preserved exactly, so every consumer built since (nav, queue, review screen, notes, workflow actions, supplier views) needed zero changes — the only interface change anywhere in `auth/` is `signIn()` losing its parameter (real sign-in doesn't let the caller choose who they are), which only touched `LoginPage.tsx`.

## 7. Revision (2026-07-27): three field-name mismatches corrected, not deferred to live verification

QA caught something the original delivery's own checklist had wrongly filed under "needs live infrastructure to verify": three of the four swapped clients used field/parameter names that were knowably wrong — verified by QA directly against the real backend source (`InvoiceDto.cs`, WP-058's `GetAll` parameters, WP-055's `InvoiceNoteDto`), not against a live token or database. This was fixable immediately, without any environment access, and has been fixed:

- **`invoiceClient.ts`** (`GET /api/invoices`): request params were `search`/`sortDirection`; real endpoint takes `invoiceNumber` (substring-only) and `sortDescending: boolean`. Fixed. **Real capability reduction, not just a naming fix**: the real endpoint has no supplier-name search at all — only an invoice-number substring match. This client's original search box behaviour (matching supplier name OR invoice number, per WP-015's fixture semantics) is not fully achievable through this endpoint as it exists today. Also added the response-DTO mapping layer this client never had (it previously trusted the wire format to already match `InvoiceListItem` exactly, via an untyped cast) — real fields are `SupplierInvoiceNumber`/`GrossTotal`/`Currency`, now correctly mapped.
- **`invoiceDetailMapping.ts`** (shared by `GET /api/invoices/{id}` and WP-054's PATCH): same three fields wrong for the same reason — `InvoiceNumber`/`Amount`/`CurrencyCode` guessed; real fields `SupplierInvoiceNumber`/`GrossTotal`/`Currency`. Fixed in the one shared mapping function both `invoiceDetailClient.ts` and `workflowActionClient.ts` depend on.
- **`invoiceNoteClient.ts`**: `author`/`timestamp` guessed (a paraphrase of a status report's prose, "id/content/author/timestamp" — never checked against the real WP-055 DTO); real fields `AuthorDisplayName`/`CreatedAtUtc`. Fixed.

**Not wrong, for contrast**: `workflowActionClient.ts`'s `targetStatusCode`/`targetStatusLabel` contract was already correct, because WP-018a had already gone through this exact reconciliation against WP-054 in an earlier revision round. The other three simply never went through that same step before this WP swapped them to real HTTP calls — the swap correctly wired up the real endpoints, but carried forward pre-ruling field-name guesses instead of checking them against source that had, in fact, already been reviewed and settled (per the README ruling referenced in QA's review).

**Added regression coverage**: dedicated unit tests for `HttpInvoiceClient`, `HttpInvoiceDetailClient`, and `HttpInvoiceNoteClient` (previously only indirectly exercised via hook/component tests) now assert the exact real field names on both request params and response mapping, so this specific class of mismatch can't silently regress. 10 new tests; full suite now 187 passing (was 177).

`docs/WP-020-Manual-Verification-Checklist.md` §§3–6 updated to remove the now-resolved field-name uncertainty — those sections now ask only for what's genuinely unverifiable without live infrastructure (real claim shapes, real PDF rendering, real role assignments), not a mismatch that was actually knowable from source already on hand.

## 8. Merge note (2026-07-26): stale-snapshot conflicts found while merging into `main` — needs Architect review

This WP-020a delivery was built from a `main` snapshot that predated several changes already merged upstream. Diffing the drop against `main` before merging surfaced multiple conflicts; per explicit Product Owner instruction, the zip's versions were taken as-is across the board rather than reconciled file-by-file. Flagging each for Architect awareness:

- **Toolchain downgrade**: `package.json`/`package-lock.json` (React 19→18, react-router-dom 7→6, Vite 8→5, Vitest 4→2, TypeScript ~6→^5.6, ESLint 10→9), `tsconfig.app.json`/`tsconfig.node.json`, `eslint.config.js`, `vite.config.ts`, `index.html` (dropped the favicon `<link>`), and `.gitignore` all reverted to an older baseline. `npm audit` now reports 12 vulnerabilities (5 moderate, 6 high, 1 critical — the critical is a dev-only `@vitest/mocker`/Vite chain issue, CWE-862, CVSS 9.8) that weren't present before this merge. **Not fixed as part of this merge** — accepted as-is per Product Owner instruction.
- **`workflowActionsByTenantAndStatus` fixture** (`src/api/fixtures/workflowActions.fixture.ts`): reverted `main`'s 2026-07-25 Chief Technical Architect-ruled correction (4 confirmed real transitions) back to the pre-ruling single-edge state, despite this delivery's own §5 above claiming the corrected fixture "was used as-is." Taken as the zip's (reverted) version per Product Owner instruction.
- **`useInvoiceQueue.ts`/`useSupplierFolderView.ts`**: reverted a hooks-pattern refactor (a documented `eslint-disable-next-line react-hooks/set-state-in-effect` workaround, with rationale, replaced by an earlier pattern) in both files identically. Taken as the zip's (reverted) version per Product Owner instruction.
- **`format.ts`'s `formatDateTime`**: dropped `timeZone: 'UTC'` from the `Intl.DateTimeFormat` options with no rationale recorded anywhere in this WP's own docs. This means invoice/note timestamps now render in each viewer's local browser timezone rather than a consistent UTC value — a real, user-visible behaviour change for an audit-relevant field, decided here only implicitly. **Taken as-is per Product Owner instruction, who asked this specific line be flagged to the Chief Technical Architect for review** — the two tests that depended on the old UTC-forced output (`format.test.ts`, `NotesList.test.tsx`) were not rewritten; instead `vite.config.ts`'s `test.env` now pins `TZ: 'UTC'` for the whole test process, so both the (now real, timezone-dependent) local-time behaviour and deterministic tests-across-environments are satisfied simultaneously without hardcoding UTC back into `format.ts` itself. **Needs a explicit ruling**: should invoice/audit timestamps display in the viewer's local timezone (current, post-merge behaviour) or a fixed UTC (`main`'s prior behaviour, and arguably more consistent for a shared audit trail per `01_Project_Context.md` §6)?
- **`jsdom` devDependency**: bumped back from the zip's `^25.0.1` to `^29.1.1` (matching `main`'s prior pin) — `^25.0.1`'s `Response.blob()` doesn't produce a Blob with a working `.arrayBuffer()` method under vitest's jsdom environment, failing `httpClient.test.ts` deterministically. This one was treated as a trivial tooling fix (version bump), not a business decision, and applied without a separate ask.

---

## AI Agent Rules acknowledgement

Per `02_Project_Standards.md` §7 and `01_Project_Context.md` §9: §1–§3's claim/DTO assumptions and §2's retry-policy defaults are reasoned defaults, flagged for sign-off once real verification is possible (checklist provided). §4 and §5 are not defaults presented as final — they are explicit statements of what could not be done without either a backend endpoint that doesn't exist (§4) or source data that wasn't provided (§5), consistent with escalating rather than guessing.
