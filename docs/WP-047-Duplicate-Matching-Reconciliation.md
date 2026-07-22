# WP-047 — Reconcile Duplicate Matching Criteria: Report

**Status:** Complete.
**Owner:** Chief Technical Architect / Product Owner (override role mapping remains provisional - see below).
**Priority:** Immediate - defect against a confirmed client requirement.

## What was changed

- **`src/APFlow.Application/Features/Invoices/DuplicateDetectionService.cs`** —
  matching rule reduced from four required fields (Supplier + Invoice Number +
  Invoice Date + Gross Amount, all AND'd) to two (Supplier + Invoice Number only).
  `HasComparableFields` now checks only `SupplierInvoiceNumber` - `InvoiceDate`/
  `GrossTotal` no longer gate whether a comparison can be made at all, matching
  their removal from the criteria itself. No date-window or amount-based fallback
  was added - none existed before, and task 2 explicitly excludes one.
- **`src/APFlow.Application/Interfaces/IDuplicateDetectionService.cs`** — doc
  comment updated to describe the two-field rule.
- **`DuplicateMatch.Reason` text** — now reads e.g. `"Matches existing invoice
  {id} on Supplier and Invoice Number ('{number}')."`, dropping the Invoice
  Date/Gross Amount clauses the old text included.
- **`src/APFlow.Application/Interfaces/IDuplicateOverrideAuthorizationService.cs`**
  / **`src/APFlow.Application/Features/Invoices/DuplicateOverrideAuthorizationService.cs`**
  (new) — the isolated `CanOverrideDuplicateWarning` permission check (task 4).
- **`src/APFlow.Application/DependencyInjection.cs`** — registered the new service.
- **`tests/APFlow.Application.Tests/Features/Invoices/DuplicateDetectionServiceTests.cs`**
  — updated/added/removed per task 5 (see below).
- **`tests/APFlow.Application.Tests/Features/Invoices/DuplicateOverrideAuthorizationServiceTests.cs`**
  (new) — tests for the override check.

## Test changes (task 5)

**Removed** (asserted the old four-field behaviour, now false):
- `CheckAsync_DifferentInvoiceDate_NotFlagged`
- `CheckAsync_DifferentGrossAmount_NotFlagged`
- `CheckAsync_CandidateMissingInvoiceDate_SkipsCheck_ReturnsNotDuplicate`
- `CheckAsync_CandidateMissingGrossTotal_SkipsCheck_ReturnsNotDuplicate`

**Renamed/updated:**
- `CheckAsync_AllFourFieldsMatch_...` → `CheckAsync_SupplierAndInvoiceNumberMatch_FlagsAsPotentialDuplicate_WithReasonRecorded`
  (asserts `MatchedFields == ["Supplier", "InvoiceNumber"]`, not the old four-item list).

**Added** (prove the removed criteria genuinely have no effect, per task 2):
- `CheckAsync_SameSupplierAndInvoiceNumber_DifferentInvoiceDate_StillFlagged`
- `CheckAsync_SameSupplierAndInvoiceNumber_DifferentGrossAmount_StillFlagged`
- `CheckAsync_CandidateMissingInvoiceDateAndGrossTotal_StillCompared` (replaces the
  two removed "skips check" tests with the opposite assertion)

**Unaffected, kept as-is:** `CheckAsync_UnknownInvoice_ReturnsFailure`,
`CheckAsync_NoOtherInvoices_ReturnsNotDuplicate`, `CheckAsync_DifferentSupplier_NotFlagged`,
`CheckAsync_DifferentInvoiceNumber_NotFlagged`,
`CheckAsync_InvoiceNumberDiffersOnlyByCaseAndWhitespace_StillFlagged`,
`CheckAsync_DoesNotMatchAgainstItself`, `CheckAsync_CandidateMissingInvoiceNumber_SkipsCheck_ReturnsNotDuplicate`,
`CheckAsync_ExistingInvoiceMissingComparisonField_NotMatchedAgainst`,
`CheckAsync_MultipleExistingDuplicates_ReturnsAllMatches`,
`CheckAsync_DoesNotModifyInvoiceOrCallSaveChanges` - all remain valid, unchanged,
under the two-field rule.

## The `CanOverrideDuplicateWarning` isolation (task 4) - confirming the design goal

The entire decision of "who may dismiss a duplicate warning" lives in exactly one
place: `DuplicateOverrideAuthorizationService.CanOverrideDuplicateWarning`, a
one-line method (`roles.Contains(Roles.FinanceManager)`). No other file in this
codebase compares a role string for this purpose - confirmed by search. If the
Product Owner's follow-up with GB Skips returns a different answer (a different
role, multiple roles, or a more complex rule), only this one method's body needs to
change; every future caller already depends on the interface, not the role name.

**No caller invokes this yet.** There is no dismiss/override endpoint or workflow
anywhere in this codebase - "Approval workflow" has been explicit out-of-scope at
every prior work package that touched invoices (WP-009, WP-010, WP-012), and WP-047
doesn't introduce one either; task 4 asks for the isolated permission check itself,
not a full override feature. The capability is registered in DI and tested in
isolation, ready for whichever future work package builds the actual dismiss
action.

**Provisional, by design:** `FINANCE_MANAGER` is the interim Full/Approver mapping
per `docs/AI/06_Domain_Reference_Data.md` §1, itself marked "interim, pending WP-051
confirmation." This method's whole reason for existing is to make that
provisionality safe to act on now without creating rework later.

## Build & Test

- `dotnet build -c Release --no-incremental` - 0 errors, 0 warnings.
- `dotnet test` (`APFlow.Application.Tests`) - all pass, including the updated
  `DuplicateDetectionServiceTests` and new `DuplicateOverrideAuthorizationServiceTests`.

## One doc deliberately NOT touched

`docs/WP-010-Duplicate-Flag-Persistence-Decision.md` still describes the original
four-field rule in its background/QA-observations sections. This report was
written as a standalone document instead of editing that one, because the copy of
that file in this delivery's working baseline is a **known-stale** pre-implementation
snapshot (still reads `Status: OPEN`, not the real repo's current `IMPLEMENTED`) -
the same staleness already caught and correctly declined during the WP-013b merge.
Editing a file I know is stale risks re-introducing exactly that regression.
Recommend whoever merges this either updates WP-010's doc separately against the
real current version, or treats this WP-047 report as the authoritative record of
the matching-criteria change until that's done.
