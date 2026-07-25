# AP Flow — Development Workflow: Addendum (WP-052 Part A)

**Status:** Approved — Permanent Reference (addendum to `05_Development_Workflow.md`)
**Audience:** All AI development agents and human engineers
**Purpose:** Records a policy addition to `05_Development_Workflow.md` established
by WP-052 Part A. Delivered as a standalone addendum file, per that work package's
own "(or a short addendum)" instruction, because `05_Development_Workflow.md`
itself is not present in this repository working copy - it has only ever been
supplied as directly-shared chat context (alongside `01_Project_Context.md`
through `06_Domain_Reference_Data.md`), never as a tracked file under this repo's
`docs/`. Whoever maintains the master copy of `05_Development_Workflow.md` should
merge the policy below into it directly; this file exists so the policy is not
lost in the meantime.

## Policy addition

**Every work package that changes the `AppDbContext` model must include a
generated EF Core migration as a deliverable - never a hand-written SQL file.**

This is now possible and enforceable: WP-052 Part A introduced the first real,
committed EF Core migration workflow for this project (previously, per
`docs/WP-046-Role-Catalogue-Remediation.md`'s own finding, no migration had ever
been generated for any prior schema change, including WP-009's Invoice/Supplier
tables, WP-012's added columns, WP-013's `AuditLogs` table, and WP-050's workflow
engine tables - all shipped without one).

Concretely, a "Provide" section asking for a schema-change deliverable should read:

> Generated EF Core migration (`Migrations/` folder under
> `APFlow.Infrastructure/Persistence/`), committed to source control - not a
> hand-written `.sql` file.

Generated via:

```
dotnet ef migrations add <Name> --project src/APFlow.Infrastructure --startup-project src/APFlow.Infrastructure --output-dir Persistence/Migrations
```

Verified via:

```
dotnet ef database update --project src/APFlow.Infrastructure --startup-project src/APFlow.Infrastructure
```

against a real (or realistically representative) SQL Server instance before the
migration is considered complete - not just generated. A `.sql` script rendering
of the same migration (via `dotnet ef migrations script`) may additionally be
included alongside the generated `.cs` migration files for easy review without
requiring the EF tooling installed, but the `.cs` files are the real, authoritative
migration - the `.sql` file is a convenience copy, not a substitute.

## Policy addition (Chief Technical Architect ruling, 2026-07-25)

**When a dependency work package is already deployed, its real contract must be
attached to the dependent work package's prompt — not left for the dependent
work package's author to independently guess.**

Raised against `docs/WP-015-Invoice-Queue-Decisions.md` item 1 and
`docs/WP-016-Invoice-Review-Decisions.md` item 2: both were built against a
fixture client and a proposed, non-binding HTTP contract on the stated grounds
that the dependency's "report/API contract was not available when built." In
both cases the dependency (WP-011 for WP-015; WP-008/WP-009/WP-011/WP-013 for
WP-016) was already deployed at the time — the real DTO/endpoint shape already
existed in the codebase. That is a coordination gap, not a genuine unknown, and
the fix is process, not code: a work package prompt for a frontend/consumer
delivery that depends on an already-shipped backend WP should include that
WP's actual DTO/endpoint shape directly, rather than letting the author
propose one in parallel. See `docs/Backlog.md` for the concrete reconciliation
items this gap produced.

## Retroactive correction to WP-046's and WP-047's records

- `docs/WP-046-Role-Catalogue-Remediation.md` - its "Files expected" discussion of
  `db/migrations/00X_correct_role_catalogue.sql` has been annotated in place (not
  rewritten) to note this policy resolves that gap - see that document's own
  "RESOLVED by WP-052 Part A" note.
- `docs/WP-047-Duplicate-Matching-Reconciliation.md` - checked directly; it never
  referenced `db/migrations/*.sql` or any migration deliverable in the first place
  (WP-047 made no schema change), so no correction was needed or made.
