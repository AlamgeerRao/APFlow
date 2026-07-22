# WP-046 — Role Catalogue Remediation: Report

**Status:** Complete for everything the codebase actually has; two items below need
Chief Technical Architect confirmation before this can be called fully closed
against the work package's original wording.
**Owner:** Chief Technical Architect.
**Raised:** WP-046 delivery.

## What was changed

- `src/APFlow.Domain/Common/Constants/Roles.cs` — replaced the five WP-002
  placeholder roles (`Administrator`/`AP Manager`/`AP Clerk`/`Finance`/`ReadOnly`)
  with the six canonical roles from `docs/06_Domain_Reference_Data.md` §1 (SA-007
  E-05): `PlatformAdmin`/`ApReviewer`/`FinanceManager`/`CreditController`/`AccountsAdmin`/`ReadOnly`.
- `src/APFlow.Api/Extensions/AuthorizationExtensions.cs` — replaced the five
  `Require*` policies with six, one per corrected role.
- `tests/APFlow.Infrastructure.Tests/Security/CurrentUserServiceTests.cs`,
  `tests/APFlow.Api.Tests/Extensions/AuthorizationExtensionsTests.cs` — updated
  every reference to the old role names.
- `docs/WP-002-Entra-Verification-Checklist.md` — updated to list the corrected
  values as what needs verifying against the real Entra App Registration (this
  checklist's own purpose - nothing here has ever been tested against a real
  tenant - is otherwise unchanged).

Confirmed via search: no reference to `Administrator`, `AP Manager`, `AP Clerk`,
`Finance` (as a role), or the old `ReadOnly` *value* (`"ReadOnly"`, as opposed to the
still-valid role concept, now `"READ_ONLY"`) remains anywhere in `src/` or `tests/`.

## Interpretation decision: constant values use SA-007 E-05's Role Code, not Role Name

WP-046's own task description lists the target roles as display names ("Platform
Administrator/AP Reviewer/..."), but `06_Domain_Reference_Data.md` provides two
columns per role - a human-readable **Role Name** and a machine-oriented **Role
Code** (e.g. `PLATFORM_ADMIN`). The actual C# constant values now use the **Role
Code** column, not the Role Name, because:

- `WP-002-Entra-Verification-Checklist.md` already flagged, before this WP existed,
  that Entra App Role "Value" fields conventionally avoid spaces, and that a
  space-containing value would cause `[Authorize(Policy = ...)]` checks to fail
  silently (403, no startup error) - exactly the shape of risk a Role Name like
  "Platform Administrator" would reintroduce.
- The Role Code column is SNAKE_CASE with no spaces, matching that convention
  directly.

Role Names are used only in doc comments/summaries for human readability. If the
real Entra App Registration's actual configured Values turn out to be something
else entirely (e.g. PascalCase without underscores), `Roles.cs`'s own doc comment
already directs updating the constants to match the Registration, not the reverse.

## Two discrepancies between the work package's wording and the actual codebase

Flagging rather than silently resolving either, per `02_Project_Standards.md` §7.

### 1. No `UserRole` table or entity exists to migrate

The work package asks to "migrate any existing `UserRole` assignments... there
should be none in a dev/test environment yet, but verify." Verified: there is no
`UserRole` entity, table, or any other persisted role-assignment concept anywhere in
this codebase. Roles are derived entirely from the `"roles"` claim of the validated
Entra JWT at request time (`ICurrentUserService.Roles`/`IsInRole`) - never written
to, or read from, Azure SQL. There is therefore nothing to migrate, and no
migration logic was written, because there is no mechanism for such a migration to
act on. This satisfies the acceptance criterion ("no remaining reference to the old
five role names") but the work package's premise (that a `UserRole` persistence
layer exists) does not match this codebase's actual state - worth confirming
whether a future work package is expected to introduce that persistence layer, or
whether role assignment is intended to remain entirely Entra-side indefinitely.

### 2. No `db/migrations/*.sql` mechanism exists in this codebase

The work package's "Files expected" lists `db/migrations/00X_correct_role_catalogue.sql`.
This codebase uses EF Core Code First (see `03_Solution_Structure.md`,
`APFlow.Infrastructure`'s `AppDbContext`) - it has no `db/migrations` folder, no raw
SQL migration scripts anywhere, and in fact no EF Core migration has ever been
generated or committed for *any* prior schema change in this project's history
(`WP-009`'s `Invoice`/`Supplier` tables, `WP-012`'s new `Invoice` columns, and
`WP-013`'s new `AuditLogs` table all shipped without one). Since roles are not
persisted data at all (see discrepancy 1 above), there is no schema/data change
this specific work package requires regardless of migration mechanism - no SQL
file was produced, because introducing a `db/migrations/*.sql` convention this
codebase has never used anywhere else would be a new mechanism, not a mechanical
completion of this work package's acceptance criteria. Confirm whether:
- a real EF Core migration workflow (`dotnet ef migrations add`) is expected to be
  introduced as part of, or before, some future work package (nothing in
  `01_Project_Context.md` through `06_Domain_Reference_Data.md` currently calls for
  one), or
- the work package's author was assuming a persistence/migration layer that does
  not yet exist in this codebase, in which case this item can be closed as N/A.

## Build & Test

- `dotnet build -c Release --no-incremental` - 0 errors, 0 warnings.
- `dotnet test` (APFlow.Api.Tests, APFlow.Infrastructure.Tests) - all pass,
  including the updated `AuthorizationExtensionsTests`/`CurrentUserServiceTests`.
