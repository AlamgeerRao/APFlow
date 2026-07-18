# WP-005 — Blob Storage Has No Tenant Isolation: Decision Required

**Status:** OPEN — not a hard gate today (no real caller uses this service yet), but
a hard gate before any real document-upload feature (e.g. invoice attachment
storage) is wired to it.
**Owner:** Chief Technical Architect.
**Raised:** WP-005 review.

## What exists today

`BlobStorageOptions.ContainerName` is a single, app-wide container shared by every
tenant. `IBlobStorageService`/`BlobStorageService` take a caller-supplied `blobName`
with no tenant-scoping mechanism at all: no prefixing by tenant, no validation
tying a blob name to `ICurrentUserService.TenantId`, nothing preventing one
tenant's code path from reading or overwriting another tenant's blob if it ever
guessed or was given the right name.

## Why this is a problem

Security Standards §4: "Protect tenant isolation at every layer where tenant data
is accessed or stored." Blob Storage is exactly such a layer once real documents
(invoice PDFs, attachments) are stored here. This is the same class of gap already
identified and tracked twice before in this project:

- WP-003: no query-level tenant filter on `TenantEntity`-derived database rows
  (`docs/WP-003-Tenant-Isolation-Decision.md`).
- WP-004: Graph mailbox config is single-tenant-shaped, doesn't scale to a second
  customer (`docs/WP-004-Graph-Multitenancy-Decision.md`).

WP-005 has the identical shape of problem and, until now, no equivalent tracked
document - an inconsistency in this project's own established discipline, not a
new category of risk.

## Why this wasn't just implemented in WP-005

The task list for this work package was "configure Blob Storage, create
BlobStorageService, upload/download/delete/SAS, add configuration" - generic
storage plumbing, explicitly not connected to invoice processing. No real feature
yet defines what tenant-scoping should look like: a per-tenant container? A
tenant-id-prefixed blob name enforced by `BlobStorageService` itself? A
per-tenant SAS scope? Any of these is a real design decision, not something to
silently guess at while building generic infrastructure with zero real callers.

## Decision needed

- [ ] Confirm the intended tenant-scoping approach: blob-name prefixing enforced
      in `BlobStorageService` (simplest, single container), per-tenant containers
      (stronger isolation, more operational overhead), or something else.
- [ ] Confirm whether `BlobStorageService` itself should validate/enforce this
      (reject a caller-supplied name that doesn't match the current tenant), or
      whether that's a responsibility of whatever future feature calls it.
- [ ] Confirm this is resolved and verified before the first real
      document-upload feature is wired to this service.

## Related

- `docs/WP-003-Tenant-Isolation-Decision.md`, `docs/WP-004-Graph-Multitenancy-Decision.md`
  - same pattern, different layer.
