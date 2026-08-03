# WP-025 – Sprint 1 Quality Assurance & System Validation (Senior QA Engineer)

Read `docs/AI` before starting, plus `docs/Backlog.md` in full (the authoritative
record of what's shipped and what's still open) and
`docs/Sprint1-Acceptance-Criteria.md` (the specific test checklist for this
sign-off — treat it as the primary instrument, not this WP's generic task list).

## Objective

Perform complete Sprint 1 verification against the system as it actually stands
today — not a fixed WP range, since the count has grown well past the original
plan. Review every entry in `docs/Backlog.md`'s Closed section as the definitive
"what's done" list.

## Perform, in this order

1. **UI/interactive testing — do this first, thoroughly, not last.** Sign in as
   both `apflow-test-approver@rameezjav.onmicrosoft.com` and
   `apflow-test-reviewer@rameezjav.onmicrosoft.com` (credentials in Key Vault
   `kv-apflow-dev-ryd3y6`) in a real browser. **Open DevTools console before
   touching anything, and keep it open for the entire session.** Click every nav
   item, every sortable column, every workflow action, for both roles. **Treat
   any red console error as an automatic defect, minimum Medium severity,
   regardless of whether the page superficially still looks fine.**
2. **Deliberately test with incomplete data, not just clean invoices.** Send at
   least one test invoice missing several fields at once (no invoice number, no
   currency, no amount) and confirm it renders with sensible fallbacks everywhere
   it appears, not just in one screen.
3. **Duplicate detection regression check (WP-080):** send the same PDF content
   via two genuinely separate emails; confirm the second is flagged as a
   possible duplicate, not silently skipped.
4. Functional testing.
5. API testing.
6. Authentication testing.
7. Database validation.
8. Blob Storage validation.
9. Microsoft Graph validation (send a real email, confirm real ingestion — don't
   rely on historical evidence alone).
10. Azure AI Document Intelligence validation.
11. Error handling verification.
12. Security review.
13. Performance sanity testing.
14. Accessibility review.
15. Regression testing — specifically re-check every incident fixed in the last
    48 hours (null-rendering crashes, email-read-before-poll, duplicate
    idempotency scoping) to confirm none have regressed.

Verify the Definition of Done for every completed work package. Do not modify
code.

## Produce

- Test Summary Report
- Defect List (Critical, High, Medium, Low)
- Risk Assessment
- Sprint Sign-off Recommendation

## Output only

`PASS`, `PASS WITH ISSUES`, or `FAIL`.
