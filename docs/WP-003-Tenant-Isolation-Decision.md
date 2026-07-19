# WP-003 — Tenant Isolation on Read: RESOLVED (WP-009)

**Status:** RESOLVED in WP-009. Kept in place as a record of the decision and its
verification status - see below for what "resolved" does and doesn't mean here.
**Owner:** Chief Technical Architect.
**Raised:** WP-003 review. **Resolved:** WP-009 (Invoice/Supplier/InvoiceNote -
the first real `TenantEntity`-derived entities, which is exactly the trigger
condition this document specified).

## What was implemented

`AppDbContext` now applies a combined tenant + soft-delete `HasQueryFilter` to
every `TenantEntity`-derived type, referencing a `_currentTenantId` instance
field directly in the filter lambda (not a captured local variable) - see
`AppDbContext._currentTenantId`'s doc comment for the full reasoning. A caller
with no resolvable tenant sees zero rows (fail-closed).

## Verification status - read this carefully before assuming this is airtight

- **Documented behavior, confirmed via Microsoft's own EF Core docs** (web
  search, post-implementation): the "instance-level field" pattern is
  Microsoft's own documented, intended solution to this exact problem
  (learn.microsoft.com/en-us/ef/core/querying/filters explicitly states filters
  referencing a DbContext field "will use the value from the correct context
  instance"). A contradictory-looking third-party source was found and
  investigated; it turned out to describe a different problem (conditionally
  adding/removing a filter based on runtime state, which does need special
  handling) rather than this codebase's scenario (an always-present filter with
  only its referenced value varying). See the code comment for the full
  reasoning on why that source's caveat doesn't apply here.
- **Written, not executed**: `AppDbContextTenantIsolationTests` constructs
  multiple `DbContext` instances (simulating different tenants) against the
  same underlying store and asserts none can see another's data - exactly the
  scenario this document originally asked to be proven. It could NOT be run
  against a real EF Core provider in the environment this was built in (no
  NuGet access anywhere in this project's history, including
  `Microsoft.EntityFrameworkCore.InMemory`). This is the single highest-priority
  test to actually execute once real tooling is available - do not treat this
  gap's resolution as fully proven until that test has actually run and passed.

## What is still NOT covered (separate, smaller follow-ups, not blocking)

- No mechanism yet for a legitimate cross-tenant use case (e.g. a future
  "platform admin sees all tenants" feature). `IgnoreQueryFilters()` exists as
  an EF Core escape hatch and is demonstrated (with a warning) in
  `AppDbContextTenantIsolationTests`, but nothing in this codebase gates who
  may use it - that's a real access-control decision for whenever such a
  feature is actually built, not something to build defensively now.

