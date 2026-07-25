# WP-056 — Persist Per-Field Extraction Confidence: Report

**Status:** RESOLVED. The one flagged scope inference below was ruled on by the
Chief Technical Architect (2026-07-25): include `Currency`, with a null
`ConfidenceScore`. Implemented accordingly. The work package's own source drop
also predated several changes already on `main` (WP-011's `Search` query
parameter, WP-055's Notes API rework) - only the files this WP actually owns
were merged; unrelated stale content in the drop was left untouched. See
"Merge notes" below.

## What this closes

`GET /api/invoices/{id}` previously returned a fixed placeholder
(`ExtractionConfidenceNote`) instead of real per-field confidence data, because
WP-052 Part D was scoped to an API endpoint over *existing* data, and `Invoice`'s
own doc comment had deliberately excluded persisting WP-008's
`ExtractedField<T>.Confidence` values pending "a real requirement". This work
package is that requirement: it adds the persistence, populates it at ingestion,
and returns it from the existing endpoint.

## Task 1 — `InvoiceExtractedField`

New child entity (`TenantEntity`-derived, same defense-in-depth reasoning as
`InvoiceNote`): `InvoiceId`, `FieldKey`, `Label`, `Value` (nullable display
text), `ConfidenceScore` (nullable `double`) - a one-to-many relationship via
`Invoice.ExtractedFields`, per the task's explicit instruction, not columns on
`Invoice` itself.

**Scope inference, ruled on:** `InvoiceExtractionResult` (WP-008) has 8 fields
total, but only 7 are wrapped in `ExtractedField<T>` (carry a confidence score)
- `Currency` is a plain `string`, reconciled from several monetary fields
rather than separately extracted/confidence-scored (see that record's own doc
comment). The work package's own source drop excluded `Currency` on the
reasoning that its value was never "discarded" (it's already persisted
unchanged on `Invoice.Currency`). **Chief Technical Architect ruling
(2026-07-25): include it anyway** - one row per `InvoiceExtractionResult`
field, always, for consistency. `BuildExtractedFields` therefore writes all 8
rows; `Currency`'s row always carries a null `ConfidenceScore`.

`InvoiceExtractedFieldKeys` (Domain constants) names all 8 fixed keys
(including `Currency`) and exposes a `CanonicalOrder` array - not
tenant-configurable data (unlike `InvoiceStatusCodes`), since it mirrors
WP-008's own fixed record shape.

## Task 2 — Pipeline population

`InvoiceProcessingService.ProcessAttachmentAsync` now calls
`BuildExtractedFields` and adds the resulting 8 rows to the not-yet-saved
`candidate.ExtractedFields` collection, at exactly the point flagged - right
after `candidate` is constructed from `extraction.*.Value`, immediately before
the existing duplicate-check/save block. This means the rows commit **atomically**
with the invoice's own insert, via the single existing `SaveChangesAsync` call -
no new commit, no new failure mode, same reasoning WP-049 already established
for why duplicate-check fields are set on the same not-yet-saved candidate
rather than persisted separately.

`Value` is a single formatted-text column (the underlying `ExtractedField<T>` is
typed per field - string/date/decimal - but nothing here needs to compute on the
value, only display it): dates as `yyyy-MM-dd`, decimals via invariant-culture
`ToString()`, both locale-independent since this is written once and never
parsed back. `Currency` is written as-is (already a plain string). A field
Document Intelligence did not extract persists as a `null` `Value` (and
independently, a `null` `ConfidenceScore` if not reported) - a normal, expected
outcome per `ExtractedField<T>`'s own doc comment, not an error that blocks
processing.

## Task 3 — `GET /api/invoices/{id}`

`InvoiceDetailResponse.ExtractionConfidenceNote` (a fixed string) is replaced
with `ExtractedFields` (`IReadOnlyList<InvoiceExtractedFieldDto>`). Kept as a
**sibling** field of the response (alongside `Invoice`/`RecentAuditEntries`),
not folded into `InvoiceDto` itself - `InvoiceDto` is reused by several other
endpoints (`available-actions`, `PATCH .../status`, `download`) that have no use
for extraction data, so a new `IInvoiceService.GetExtractedFieldsAsync` was
added and wired into `InvoicesController.BuildDetailResponseAsync` as a second,
parallel fetch alongside the existing audit-history one - same
fetch-separately, fail-soft-to-empty-list-on-error pattern already established
there for `RecentAuditEntries`.

Returned in `InvoiceExtractedFieldKeys.CanonicalOrder`, not row/insertion order
- EF Core/SQL do not otherwise guarantee an order for these rows.

Empty (not missing, not an error) for an invoice created before WP-056 or not
processed via the WP-012 pipeline at all (e.g. created manually) - this is a
normal, expected state for such an invoice.

## Merge notes

This WP's own source drop (like WP-055's before it) was a snapshot that
predated several changes already on `main` - among them WP-011's `Search`
query parameter (`InvoiceQueryParameters`/`InvoiceRepository`/`InvoiceQueryService`)
and WP-055's Notes API rework (`ICurrentUserService.DisplayName` as a required
member, `AuthorDisplayName` at `nvarchar(200)`). Rather than overwrite files
wholesale (which would have silently regressed that already-shipped work), only
the files this WP's own decision doc names as created/modified were merged, and
each modified file's WP-056-specific delta was applied by hand onto the current
`main` version rather than replacing the file outright. Files the drop touched
outside its own declared scope (frontend files, unrelated `docs/WP-*` files,
`InvoiceQueryService.cs`/`InvoiceListItemDto.cs`/`InvoiceQueryParameters.cs`,
`CurrentUserService.cs`, `.claude/settings.local.json`, etc.) were left
untouched.

The drop's own EF migration file
(`20260725101649_AddInvoiceExtractedFields.*`) and its accompanying stale
duplicate of WP-055's already-resolved `AddInvoiceNoteAuthorDisplayName`
migration were both discarded rather than copied in - regenerating fresh via
`dotnet ef migrations add` against the current `main` model (which already
includes WP-055's `nvarchar(200)` column) avoids any risk of migration-history
drift or a second, conflicting definition of the same column. The resulting
migration is `20260725111332_AddInvoiceExtractedFields`.

## Files created

- `src/APFlow.Domain/Entities/InvoiceExtractedField.cs`
- `src/APFlow.Domain/Common/Constants/InvoiceExtractedFieldKeys.cs` (8 keys,
  including `Currency` per the ruling above)
- `src/APFlow.Application/DTOs/InvoiceExtractedFieldDto.cs`
- `src/APFlow.Infrastructure/Persistence/Configurations/InvoiceExtractedFieldConfiguration.cs`
- `src/APFlow.Infrastructure/Persistence/Migrations/20260725111332_AddInvoiceExtractedFields.cs`
- `src/APFlow.Infrastructure/Persistence/Migrations/20260725111332_AddInvoiceExtractedFields.Designer.cs`
- `src/APFlow.Infrastructure/Persistence/Migrations/20260725111332_AddInvoiceExtractedFields.sql`
  (convenience rendering, via `dotnet ef migrations script`, generated
  freshly against `main` rather than copied from the source drop - see
  "Merge notes")
- `docs/WP-056-Persist-Extraction-Confidence-Decisions.md` (this file)

## Files modified

- `src/APFlow.Domain/Entities/Invoice.cs` - new `ExtractedFields` navigation
  property; class doc comment updated to note this closes the gap it originally
  flagged ("Add if/when that's a real requirement")
- `src/APFlow.Infrastructure/Persistence/AppDbContext.cs` - new
  `InvoiceExtractedFields` `DbSet`
- `src/APFlow.Infrastructure/Persistence/Migrations/AppDbContextModelSnapshot.cs` -
  regenerated by `dotnet ef migrations add` against the current `main` model
- `src/APFlow.Application/Interfaces/IInvoiceRepository.cs` - new
  `GetByIdWithExtractedFieldsAsync`
- `src/APFlow.Infrastructure/Persistence/InvoiceRepository.cs` - implements it
  (`.Include(i => i.Supplier).Include(i => i.ExtractedFields)`, same shape as
  the existing `GetByIdWithNotesAsync`); WP-011's `Search` filter block
  (already on `main`) preserved as-is
- `src/APFlow.Application/Interfaces/IInvoiceService.cs` - new
  `GetExtractedFieldsAsync`
- `src/APFlow.Application/Features/Invoices/InvoiceService.cs` - implements it
  (sorts by `CanonicalOrder`); new `ToExtractedFieldDto` mapper
- `src/APFlow.Application/Features/Invoices/InvoiceProcessingService.cs` -
  populates `candidate.ExtractedFields` at the point flagged; new
  `BuildExtractedFields`/`NewExtractedField`/`FormatDate`/`FormatDecimal` helpers
  (`BuildExtractedFields` includes `Currency` per the ruling above)
- `src/APFlow.Api/Contracts/InvoiceDetailResponse.cs` - `ExtractionConfidenceNote`
  replaced with `ExtractedFields`
- `src/APFlow.Api/Controllers/InvoicesController.cs` - `BuildDetailResponseAsync`
  fetches real extracted-field data instead of returning the placeholder string
- `tests/APFlow.Application.Tests/Features/FakeRepositories.cs` -
  `FakeInvoiceRepository` implements the new repository method
- `tests/APFlow.Application.Tests/Features/Invoices/InvoiceProcessingServiceTests.cs` -
  two new tests (confidence persisted correctly, including `Currency` with a
  null `ConfidenceScore`; a not-extracted field persists as `null`, not an error)
- `tests/APFlow.Application.Tests/Features/Invoices/InvoiceServiceTests.cs` -
  three new tests (`GetExtractedFieldsAsync` canonical ordering, empty-not-error,
  not-found)
- `tests/APFlow.Api.Tests/Controllers/InvoicesControllerTests.cs` -
  `FakeInvoiceService` extended with `GetExtractedFieldsAsync`; the existing
  `GetById` test updated to assert against `ExtractedFields` instead of the
  removed `ExtractionConfidenceNote`
- `README.md` - added WP-056 row; corrected the WP-052 open-decisions bullet
  (the extraction-confidence item it flagged is now closed by this work package)

## Migration

Generated (not hand-written), per `05_Development_Workflow_Addendum.md`'s
convention, freshly against the current `main` model (see "Merge notes"):

```
dotnet ef migrations add AddInvoiceExtractedFields --project src/APFlow.Infrastructure --startup-project src/APFlow.Infrastructure --output-dir Persistence/Migrations
```

A single new table (`InvoiceExtractedFields`), with a foreign key to `Invoices`
(cascade delete) and the same two indexes `InvoiceNoteConfiguration` uses
(`InvoiceId` alone, plus `(TenantId, InvoiceId)`) - no alteration to any
existing table, no data-loss warning.

**Verified against a real, running SQL Server** (`localhost\SQLEXPRESS` on
this machine - see `docs/Backlog.md`/local dev notes for why not
`(localdb)\mssqllocaldb`): `dotnet ef database update` applied cleanly, and the
live `InvoiceExtractedFields` table's columns were independently confirmed via
`INFORMATION_SCHEMA.COLUMNS` to match the migration exactly (`FieldKey`
`nvarchar(100)`, `Label` `nvarchar(200)`, `Value` `nvarchar(4000)` nullable,
`ConfidenceScore` `float` nullable).

## Tests

- `InvoiceProcessingServiceTests` (Application): the happy path now also asserts
  all 8 fields are persisted with correct `FieldKey`/`Label`/formatted
  `Value`/`ConfidenceScore` - 7 confidence-scored fields plus `Currency` with a
  null `ConfidenceScore`; a not-extracted field persists as `null`/`null`, not a
  processing failure.
- `InvoiceServiceTests` (Application): `GetExtractedFieldsAsync` returns fields
  sorted into canonical order (asserted against a deliberately scrambled
  insertion order, so the sort is actually exercised, not just coincidentally
  correct); returns an empty list (not a failure) when none exist; returns
  `Invoice.NotFound` for a missing invoice.
- `InvoicesControllerTests` (Api): the existing `GetById` test now asserts the
  response's `ExtractedFields` carries real data instead of the removed
  placeholder note.

## Build & Test

- `dotnet build -c Release --no-incremental` - 0 errors, 0 warnings, whole solution.
- `dotnet test` across all 5 test projects - **320/320 pass**:
  `APFlow.Domain.Tests` 11, `APFlow.Application.Tests` 149 (+5 new),
  `APFlow.Api.Tests` 40, `APFlow.Infrastructure.Tests` 75,
  `APFlow.Integrations.Tests` 45.
- `/health/live` → 200; `/health/ready` → 200, `Degraded` (Graph/Blob Storage
  unconfigured in this dev sandbox, per WP-004's severity ruling - not
  `Unhealthy`; database connectivity independently confirmed by the migration
  verification above).
