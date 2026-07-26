# WP-020 — Manual Verification Checklist

**Why this exists:** WP-020's own deliverable list asks for "confirmation each swapped screen works against the real API in a local/dev environment." That requires a real (or dev-tier) Entra External ID tenant and a reachable running `APFlow.Api` instance — neither is available in the environment this WP was built in (no backend repo checked out, no live API URL, no Entra client ID/authority/tenant ID, per the Chief Technical Architect's own confirmation before this WP started). Everything below was verified as far as it's possible to without those: unit tests (187 passing, see the WP-020 delivery summary) mock `httpClient`/MSAL directly and exercise the actual request-building, response-mapping, and error-handling code paths — including, as of a 2026-07-27 revision, dedicated tests locking in the correct real DTO field names (`docs/WP-020-Real-Auth-And-Api-Integration-Decisions.md` §7) after QA caught three field-name mismatches that were knowable from backend source without live access. What's left below is genuinely live-environment-only verification — this checklist is for whoever has that access (DevOps / the Chief Technical Architect / a future session with real credentials).

---

## 0. Prerequisites

- [ ] `.env.local` created from `.env.example` with real values for `VITE_ENTRA_CLIENT_ID`, `VITE_ENTRA_AUTHORITY`, `VITE_ENTRA_REDIRECT_URI`, `VITE_API_SCOPE`, `VITE_API_BASE_URL`.
- [ ] `APFlow.Api` running and reachable at `VITE_API_BASE_URL`, with a real database migrated (`dotnet ef database update`) and seeded per WP-050/053's WorkflowTemplate data.
- [ ] A real Entra External ID app registration exists for `APFlow.Web`, with the API's scope exposed and consented.
- [ ] At least one real test user assigned the `AP_REVIEWER` role and one assigned `FINANCE_MANAGER` (per `06_Domain_Reference_Data.md` §1), ideally for the GB Skips tenant specifically, to exercise role-gated actions.

## 1. Sign-in (task 1)

- [ ] Visiting the app while signed out redirects to `/login`; clicking "Sign in with Microsoft" redirects to the real Entra sign-in page (not a demo picker).
- [ ] After a successful sign-in, the app lands back on the originally-requested route (or `/dashboard` if none), and the header shows the real signed-in user's name/tenant.
- [ ] `deriveActingUser`'s assumptions hold against a real token: confirm in devtools (`sessionStorage`, MSAL's cached account) that `tenantId` matches the expected AP Flow tenant, `roles` contains the expected app roles, and `displayName` is sensible. **If any of these don't match** (see `deriveActingUser.ts`'s own doc comment for exactly which claims are assumed), that's expected per WP-002's own Verification Checklist — fix the mapping, not just this checklist.
- [ ] Sign-out returns to a signed-out state and a subsequent protected-route visit redirects to `/login` again.

## 2. Central API client / global behaviour (tasks 2–3)

- [ ] Open Network tab: every API request carries `Authorization: Bearer <token>`.
- [ ] Force a 401 (e.g. revoke/expire the session server-side) and confirm the app redirects to sign-in again rather than showing a raw error.
- [ ] Simulate a transient failure (e.g. stop the API briefly, or throttle network) during a GET — confirm the app retries rather than failing on the first attempt (watch the Network tab for repeated requests to the same URL).
- [ ] Confirm a POST/PATCH does **not** auto-retry on failure (submit a note or workflow action while the API is down — should fail once, not retry silently, avoiding a possible double-submit).

## 3. WP-015 — Invoice Work Queue

- [ ] `/invoices` loads real invoices from `GET /api/invoices` (WP-058). Field names are now confirmed correct (`invoiceNumber`→`supplierInvoiceNumber`, `amount`→`grossTotal`, `currencyCode`→`currency` — see `docs/WP-020-Real-Auth-And-Api-Integration-Decisions.md` §7), so this should just work — if it doesn't, something else is wrong, not the field mapping.
- [ ] **Known, accepted capability reduction, confirm it's acceptable in practice**: the search box now only matches invoice number (substring), not supplier name — the real `GetAll` endpoint has no supplier-name search parameter at all. If supplier-name search turns out to matter in practice, that's a backend feature request, not a frontend bug.
- [ ] Status filter, sort, and pagination all issue new requests with the expected query params and re-render correctly.
- [ ] Duplicate-flagged invoices still highlight correctly.

## 4. WP-016 — Invoice Review Screen

- [ ] Clicking a queue row navigates to `/invoices/review/:id` and loads real detail from `GET /api/invoices/{id}`. Field names are now confirmed correct (`invoiceNumber`→`supplierInvoiceNumber`, `amount`→`grossTotal`, `currencyCode`→`currency` — see `docs/WP-020-Real-Auth-And-Api-Integration-Decisions.md` §7). The remaining fields on `InvoiceDetailResponseDto` (`id`, `supplierName`, `invoiceDate`, `status`, `isPotentialDuplicate`, `duplicateCheckReason`, `sourceDocumentBlobName`, `createdAtUtc`) were not specifically re-checked against source and are still worth a first-time confirmation.
- [ ] The PDF viewer actually renders a real invoice PDF (confirms both `GET /api/invoices/{id}/download` and the blob-URL workaround in `HttpInvoiceDetailClient` — see its doc comment for why a direct URL wouldn't work).
- [ ] Navigate Previous/Next across several invoices and confirm no memory growth from leaked blob URLs (devtools → Memory, or just confirm `URL.revokeObjectURL` is being hit — `useInvoiceDetail.ts`'s cleanup logic).
- [ ] Confirm `extractedFields` render with real per-field confidence scores (WP-056) — specifically confirm a `Currency` field renders "Not available" (null score), not a percentage or a crash.
- [ ] `overallConfidenceScore` is computed client-side (mean of non-null field scores) — sanity-check it against the individual field scores shown; there is no server-supplied aggregate to compare against.

## 5. WP-017 — Notes

- [ ] Existing notes load from `GET /api/invoices/{id}/notes`. Response field names are now confirmed correct (`authorDisplayName`/`createdAtUtc`, not `author`/`timestamp` — see decision doc §7).
- [ ] Adding a note POSTs `{ content }` only (check the Network tab — **no** author field should be sent at all, not just an ignored one — `HttpInvoiceNoteClient.addNote` has no parameter to send one) and the author shown afterward is the real signed-in user's name, resolved server-side.

## 6. WP-018 — Workflow Actions

- [ ] Available actions shown match `GET /api/invoices/{id}/available-actions`'s real response for the invoice's actual status and the signed-in user's actual role — cross-check against WP-053's confirmed transition graph if available. (This contract — `targetStatusCode`/`targetStatusLabel` — was already correct going into this WP; it's the one client that didn't need a field-name fix.)
- [ ] As a `FINANCE_MANAGER`-role user: confirm all four real role-gated transitions are available at the right statuses (`CHECKED_READY_TO_APPROVE→APPROVED`, `CHECKED_READY_TO_APPROVE→NEEDS_QUERY`, `REJECTED→AWAITING_REVIEW`, `CANCELLED→RECEIVED`).
- [ ] As an `AP_REVIEWER`-role user: confirm those four are absent, and a non-gated transition still works.
- [ ] Execute a real transition and confirm the UI updates immediately from the PATCH response (no second network request/spinner) — this is the specific behaviour WP-020 changed from WP-018's original `retry()`-based design. The response uses the same (now-fixed) `InvoiceDetailResponseDto` shape as §4.
- [ ] Attempt a transition your role shouldn't be permitted for (if reachable, e.g. via a stale render) and confirm the 403's `code` field maps to a specific, readable message, not a generic error.

## 7. WP-019 — Supplier & Folder Views

- [ ] **Not applicable yet.** This WP is still fixture-backed — no real backend endpoint exists for it (see `supplierFolderClient.ts`'s own doc comment and §4 of the decision doc). Nothing to verify here until a backend WP builds the relevant endpoints.

## 8. Cross-cutting

- [ ] Every screen above still renders its loading and error states sensibly against real (slow/failing) network conditions, not just the instant-resolving fixture promises they were built and tested against.
- [ ] No console errors/warnings beyond the known React Router v7-future-flag advisories already present in the test suite's output.
