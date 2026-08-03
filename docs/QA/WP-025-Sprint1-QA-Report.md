**PASS WITH ISSUES**

# WP-025 — Sprint 1 Quality Assurance & System Validation

**QA pass conducted:** 2026-08-03
**Against commit:** `b7d0932` (`origin/main` HEAD at pass start) — deployed application code is functionally equivalent to `be0a7de` (the last commit that actually triggered `deploy-api`/`deploy-web`; everything after it was docs-only and correctly skipped redeployment per WP-078's path-based filtering)
**Pipeline state:** last real GitHub Actions run green (`be0a7de`, all jobs succeeded)
**Method:** real browser automation (Chrome, via `claude-in-chrome`) against the live dev environment, real CIAM sign-in as both test accounts, real workflow-action execution (not simulated), direct Azure SQL queries, direct Application Insights KQL queries, direct unauthenticated/authenticated API calls. This is the **first genuine browser-driven UI walkthrough** this system has ever had — every prior "live-verified" claim in `docs/Backlog.md` was proven via API calls, deployed-bundle inspection, or direct database/log queries, never an actual rendered page with a real user token.

---

## Test Summary Report

### Pre-conditions (Sprint1-Acceptance-Criteria.md)

| Pre-condition | Result |
|---|---|
| Document Intelligence resource live, Managed Identity | Confirmed via prior WP-061 evidence (not re-tested; unchanged since) |
| API runs in real (non-Development) config | Confirmed — `/health/ready` returns `Healthy`, no `ASPNETCORE_ENVIRONMENT` override |
| Real end-to-end ingestion, confirmed fresh | **Not re-sent this pass** — no Graph mailbox send capability available in this session. Relied on real, pre-existing evidence instead (see Defect HIGH-1 below, itself discovered via a real, naturally-occurring email) |
| Pipeline fully green, no manual intervention | Confirmed — last run (`be0a7de`) green, all jobs succeeded |
| Two CIAM test users, different roles, real tokens | Confirmed — both signed in fresh via real interactive CIAM flow this pass (approver's session was cached from a prior sign-in; reviewer's was a completely fresh interactive sign-in, first one this project has ever had) |

### 1. Entra authentication and role-based access

| Item | Result |
|---|---|
| Real browser sign-in via CIAM | **PASS** — fresh interactive sign-in as reviewer completed successfully end-to-end, first time ever confirmed in a real browser |
| Unauthenticated request → 401 | **PASS** — confirmed via direct `curl` |
| Header shows "Signed in as: Full Approver" / "Standard Reviewer" | **FAIL** — see Defect MEDIUM-1 |
| Approver sees + can execute Approve; Reviewer doesn't see the button at all | **PASS** — confirmed both directions live: approver executed a real Approve action end-to-end; reviewer's UI genuinely omits the button (not disabled, absent from the DOM) on an invoice at the same status |
| Reviewer's direct API PATCH attempt → 403 | **PASS** — real `403 Approval.Unauthorized`, invoice confirmed unchanged |
| Session expiry behavior | Not tested (would require waiting out a real token lifetime; out of scope for this pass's time budget) |

### 2–5. Mailbox, email reading, PDF extraction, Blob storage

| Item | Result |
|---|---|
| Fresh end-to-end ingestion test | **Not performed** — no mailbox send capability this session (see Pre-conditions above) |
| Non-PDF attachment ignored / no-attachment email → Inbox | **PASS** (with a major caveat) — Inbox genuinely shows real flagged emails with sender/subject/date; the "same thread increments occurrence count" mechanism visibly works (one row, not duplicates) — **but see Defect HIGH-1**: this same mechanism has a real bug that let it run away to 719 occurrences on one thread |
| Content-hash idempotency | Not independently re-tested this pass; extensively proven in `docs/Backlog.md` (WP-052, WP-080) |

### 6. Document Intelligence extraction

| Item | Result |
|---|---|
| Extracted fields display with confidence, including Currency's blank confidence | **PASS** — confirmed live on two real invoices (BentsonSolvers: VAT at 48%/Low, everything else High; DigitalOcean: every extractable field genuinely absent, Currency correctly shows "Not available") |
| Confidence badge colour thresholds (≥0.85 high / 0.6–0.84 medium / <0.6 low) | **PASS** — BentsonSolvers' VAT at 48% correctly rendered "Low confidence — recommend manual verification"; other fields at 88–97% correctly "High confidence" |

### 7. Persisting invoices in Azure SQL

| Item | Result |
|---|---|
| `GET /api/invoices/{id}` via UI shows all fields | **PASS** |
| Invoice Queue list/search/filter/sort/pagination against real data | **PASS** — including WP-077's Invoice-Number/Amount sort fix, re-confirmed live with zero console errors and zero 400s |

### 8. Duplicate detection

| Item | Result |
|---|---|
| Two invoices, same Supplier+Number, flagged with link | **PASS** — a real duplicate pair (AL, `2MWCNWLI-0001`) visible in Invoice Queue with the "Possible duplicate" flag; link not clicked through this pass (already proven in WP-073) |
| Flagged invoice not blocked from workflow | **PASS** (inferred from the flagged pair both sitting normally in Awaiting Review, same as any other invoice) |

### 9. React web interface

| Item | Result |
|---|---|
| Invoice Queue search/filter/sort/pagination | **PASS** |
| Invoice Review: fields, confidence, PDF viewer, audit history, Prev/Next | **PASS** — PDF `Open in new tab`/`Download` blob links present and correctly formed |
| Notes: add, correct author/timestamp, no edit/delete | **PASS** — confirmed via real historical notes from three different real identities (a genuine past human user, and both test accounts), each correctly server-resolved |
| Supplier & Folder Views show real data | **PASS** — 8 real suppliers, correct per-status counts, no fixture placeholders |
| Query Queue shows combined statuses correctly | **PASS** — real `NEEDS_QUERY` invoice shown |
| "Approved" not a standalone nav link | **PASS** — confirmed absent from the static top-level nav; still reachable as one of Invoice Queue's own per-status sub-links (expected, not a violation) |

### 10. Manual workflow management

| Item | Result |
|---|---|
| Nav's Invoice Queue sub-links show all 15 GB Skips statuses | **See Note LOW-1** — nav shows 14; this is deliberate existing design (WP-014: nav only lists *non-terminal* statuses), not a new bug. `ARCHIVED` (the 15th, terminal) is correctly excluded from the *nav*, though the underlying `workflow-template` API endpoint does return all 15 (confirmed directly against the database) |
| Actions reflect only valid transitions for status+role | **PASS** — confirmed on 3 different invoices across both roles |
| Full flow `AWAITING_REVIEW → CHECKED_READY_TO_APPROVE → APPROVED` | **PASS** — executed live, end-to-end, real state transitions, real audit trail, both gates enforced correctly |
| `AWAITING_REVIEW → APPROVED` directly not possible | **PASS** — reviewer's own available-actions list at `AWAITING_REVIEW` never included `APPROVED` |
| Escalation / reopen paths | **Partially confirmed** — `AWAITING_REVIEW → NEEDS_REVIEW_FEBINA → CHECKED_READY_TO_APPROVE` confirmed via real historical audit trail on the BentsonSolvers invoice; `REJECTED → AWAITING_REVIEW` seen as an available action on a real Rejected invoice (as approver) but not clicked through to completion this pass |
| Invalid/role-gated transition rejected, invoice unchanged | **PASS** — the reviewer's direct 403 attempt above left the invoice's status genuinely unchanged |

### 11. Notes and audit history

| Item | Result |
|---|---|
| Every status change / note addition produces an audit entry | **PASS** — confirmed on every invoice inspected |
| Append-only | Not adversarially tested (no delete/edit endpoint found in the UI to attempt) |
| Recent audit entries display correctly | **PASS** |

### 12. Deployment pipeline, monitoring, logging

| Item | Result |
|---|---|
| Deployed commit matches `origin/main`, last pipeline run succeeded | **PASS**, with the WP-078 nuance noted at the top of this report |
| `/health/live` → 200 Healthy; `/health/ready` → 200, database Healthy | **PASS** — both fully `Healthy` at test time (Graph/Blob included, better than the documented "Degraded may be expected" baseline) |
| Monitoring dashboard genuinely useful | **Not independently re-confirmed visually in the Portal this pass** — equivalent evidence gathered instead via direct KQL queries against the same Application Insights data throughout this session (see Defect HIGH-1's investigation) |
| App Insights shows real telemetry from this pass's own activity | **PASS** — this pass's own SQL health-check failures, duplicate-detection traces, and the historical `IngestionIssue` traces were all directly queried and confirmed real |

---

## Defect List

### HIGH-1 — Ingestion-issue "no processable attachment" emails can be reprocessed indefinitely, defeating the WP-076/read-before-poll dedup mechanism

**What was found:** A single real email (`Fwd: Reservation Confirmation`, no PDF attachment, only inline images) was reprocessed by `EmailIngestionWorker` **719 times** between 2026-08-01 23:39 UTC and 2026-08-02 12:00 UTC — roughly matching the 60-second poll interval for that ~12.4-hour window. Confirmed via:
- Direct SQL query: `IngestionIssues` row for this thread shows `OccurrenceCount = 719`.
- Direct Application Insights KQL: the exact log line `"Email {same MessageId} ... had no processable PDF attachment"` appears 719 times, always the *same* message ID (not 719 distinct emails).
- Zero log evidence — success, "already marked," or failure — that `MarkAsProcessedAsync` was ever invoked for this specific message ID, despite `ProcessEmailAsync`'s code path for "zero PDF attachments" unconditionally returning `readyToMark = true`, which should trigger it.

**Current state, as of this QA pass:** the loop is **not currently active** — `LastSeenUtc` has not advanced past 2026-08-02 12:00:59 UTC, over 24 hours before this pass. It appears to have stopped on its own (possibly coinciding with one of that day's several redeploys restarting the worker process), not because the underlying cause was fixed.

**Why this matters:** this directly defeats the purpose of the `7bb3e04` "emails read before the poll cycle were silently never ingested" fix and its category-based dedup mechanism. Left unnoticed, this is 719 avoidable Graph API calls (rate-limit/cost risk) and 719 avoidable database writes for a single email thread — and nothing alerts on it; `RecordIngestionIssueAsync`'s own doc comment explicitly frames this as an intentionally-swallowed, non-alerting path.

**Root cause: not fully pinned down this pass.** Two live hypotheses, not distinguished with the diagnostic access available in this session:
1. `MarkAsProcessedAsync` is genuinely never being invoked for this message (a bug upstream of it).
2. It is invoked and succeeds, but the Graph category doesn't durably exclude the message from the next `not(categories/any(...))` query for this specific message (a Graph-side or query-side issue) — and its own success log line simply isn't showing up in the sampled telemetry.

**Recommendation:** direct Graph API access to the mailbox (checking this message's actual live category list) would resolve which hypothesis is correct; not available in this session. Given real financial/compliance stakes are low here (no invoice data is lost or corrupted — this is a "nothing to process" email, correctly identified as such every single time), this doesn't block sign-off, but it's a real, confirmed, previously-undetected defect that could recur silently on the next similarly-shaped email.

### MEDIUM-1 — Header's "Signed in as: {role}" label never renders, for any user

**What was found:** `Header.tsx` reads `user.roles` (from `deriveActingUser`, which reads `account.idTokenClaims.roles`) to build the "· Signed in as: Full Approver" suffix. Decoded the real, live ID token from an authenticated session: it carries **no `roles` claim at all** (`aud` = the SPA client). The real access token issued in the same login (`aud` = the API client) **does** carry `roles: ["FINANCE_MANAGER"]` correctly — Entra only includes app-role claims in tokens issued for the resource (audience) the role is assigned against, and these roles were assigned against the API's service principal (WP-064), not the SPA's.

**Impact:** cosmetic only. Confirmed separately that no actual authorization logic depends on this — `WorkflowActionsPanel`/`useWorkflowActions` call the real `available-actions` API (using the correctly-populated access token) and were confirmed, live, to gate correctly in both directions. This bug affects only the header's own display text.

**Why this was never caught:** `deriveActingUser.ts`'s own doc comment already flagged this exact claim shape as "UNCONFIRMED against a real issued token" since WP-020; WP-076's Backlog entry described this as "sourced from the existing role claim already used for action gating" — that framing turns out to be incorrect (it's a *different* token/claim source from the one action-gating actually uses), which is exactly why no one caught it without a real browser session and a real decoded token.

**Fix direction (not implemented this pass, per WP-025's "do not modify code"):** derive the header's role label from the access token's `roles` claim instead of the ID token's, or from the same `available-actions`/role data already being fetched for action gating.

### LOW-1 — Nav shows 14 of GB Skips' 15 statuses; discrepancy against written acceptance-criteria wording (not a code defect)

**What was found:** `06_Domain_Reference_Data.md` confirms GB Skips' full status set is 15 (13 platform-wide + 2 tenant additions), including `ARCHIVED` (terminal). Confirmed directly against the database: all 15 `StatusReferences` rows exist correctly. The Invoice Queue's nav sub-links show only 14 — `ARCHIVED` is missing, **by design**: `navConfig.ts`'s `buildInvoiceQueueLinks` deliberately filters `!status.isTerminal`, a WP-014-era decision (one nav link per *actionable* status, not a decorative complete list) that predates and is unrelated to WP-075's fix.

**Why this is flagged, not just noted:** `Sprint1-Acceptance-Criteria.md` explicitly says "The nav's Invoice Queue sub-links show all 15 GB Skips statuses in the correct order" — that's not literally true today, and never has been, for a deliberate and reasonable reason. This reads like the acceptance-criteria author conflated "the API returns all 15" (WP-075's actual, correct deliverable) with "the nav displays 15 links" (never WP-014's design). Recommend the Chief Technical Architect either (a) corrects the acceptance criteria's wording, or (b) rules that the nav should in fact show all 15 including terminal ones — a real, small, discrete follow-up either way, not something QA should silently resolve either direction.

---

## Risk Assessment

- **Security/authorization:** no findings. Role gating is correctly enforced at both the UI layer (confirmed both directions, live) and the API layer (confirmed via a real unauthorized 403), independent of the MEDIUM-1 cosmetic bug.
- **Data integrity:** no findings. Every invoice inspected had a correct, complete, append-only audit trail; the core approval workflow was executed live end-to-end without any inconsistency.
- **Reliability:** HIGH-1 is a real, if currently dormant, gap — a background-worker dedup mechanism that can silently fail open into unbounded reprocessing, with no alerting. Worth a follow-up WP to pin down the actual root cause (this pass's investigation narrowed it to two hypotheses but couldn't distinguish them without direct Graph API access) and, separately, to consider alerting on abnormally high `IngestionIssue.OccurrenceCount` values.
- **User experience:** MEDIUM-1 is real but low-stakes — every user currently sees their name but never their role label, which is a legitimate small trust/clarity gap for a finance-adjacent tool but not a functional blocker.
- **Documentation accuracy:** LOW-1 shows the acceptance criteria itself needs a small correction or a real ruling — worth closing so it doesn't cause confusion on a future QA pass.

## Sprint Sign-off Recommendation

**PASS WITH ISSUES.**

The system's core value — real Entra auth, real role-gated two-tier approval workflow, real Document-Intelligence-extracted data with correct confidence signaling, real duplicate detection, real audit trail, real Blob-backed document access — all held up under this project's first genuine browser-driven test, including two live state-changing workflow actions executed end-to-end. Nothing found here touches security or data integrity. One Medium cosmetic defect (MEDIUM-1) and one confirmed-but-currently-dormant reliability gap (HIGH-1) are real, novel findings this pass exists to surface — exactly the kind of UI/live-session-only bugs the acceptance criteria's own framing anticipated finding. Recommend both be raised as follow-up WPs; neither blocks Sprint 1 sign-off.
