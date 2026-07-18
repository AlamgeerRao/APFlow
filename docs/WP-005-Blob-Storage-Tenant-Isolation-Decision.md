# WP-005 — Blob Storage Has No Tenant Isolation: Decision Required

**Status:** RESOLVED — blob-name prefixing enforced inside `BlobStorageService`,
single shared container. Implemented and verified (see "Resolution" below).
**Owner:** Chief Technical Architect.
**Raised:** WP-005 review.
**Decided:** 2026-07-18.

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

- [x] Confirm the intended tenant-scoping approach: blob-name prefixing enforced
      in `BlobStorageService` (simplest, single container), per-tenant containers
      (stronger isolation, more operational overhead), or something else.
      **Decided: blob-name prefixing, single shared container.**
- [x] Confirm whether `BlobStorageService` itself should validate/enforce this
      (reject a caller-supplied name that doesn't match the current tenant), or
      whether that's a responsibility of whatever future feature calls it.
      **Decided: `BlobStorageService` enforces it itself, transparently.**
- [x] Confirm this is resolved and verified before the first real
      document-upload feature is wired to this service. **Done - see below.**

## Resolution

**Approach:** every caller-supplied `blobName` passed to `IBlobStorageService` is a
*logical* name, scoped to the single shared container. `BlobStorageService`
transparently prefixes it with the current caller's tenant id
(`{ICurrentUserService.TenantId}/{blobName}`) before it ever reaches Azure
Storage. Callers never see or supply the prefix and cannot construct a path that
addresses another tenant's blob through this interface - the tenant id comes from
the validated JWT via `ICurrentUserService`, not a caller-supplied argument, the
same way `AppDbContext` stamps `TenantId` on write rather than trusting each
caller to set it correctly.

**Why blob-name prefixing over per-tenant containers:** matches the current
single-container configuration shape (`BlobStorageOptions.ContainerName`) with no
added operational overhead (no per-tenant container provisioning/lifecycle to
manage). SAS URLs are already generated per-blob (`GenerateSasUriAsync`), which is
strictly tighter than a container boundary would provide - a SAS can only ever be
minted for a path already prefixed with the caller's own tenant id. Nothing about
this closes the door on per-tenant containers later if a real requirement (e.g.
per-tenant encryption keys or independent retention policies) emerges - the public
interface exposes only logical names, so the physical layout can change without an
API change.

**Why `BlobStorageService` enforces it, not callers:** the entire point of this
decision doc was that leaving it to "whichever future feature calls it" is exactly
how this gets silently forgotten once. Centralizing it in `BlobStorageService`
means every current and future caller gets it for free and cannot opt out.

**Implementation note - DI lifetime changed Singleton → Scoped:** enforcing this
requires `ICurrentUserService` (Scoped - depends on the current `HttpContext`) as a
constructor dependency of `BlobStorageService`. A `Singleton` registration would
have captured whichever tenant's request happened to construct the singleton first
and frozen to that tenant for the process lifetime - the identical bug class
flagged in `docs/WP-003-Tenant-Isolation-Decision.md` for the EF Core query filter
(compiled-model caching capturing per-request state). `BlobStorageService` is now
registered `Scoped` in `APFlow.Infrastructure.DependencyInjection.AddBlobStorage`;
`BlobServiceClient`/`IBlobContainerOperations` remain `Singleton` (thread-safe,
stateless).

**Verified by:** `tests/APFlow.Infrastructure.Tests/Storage/BlobStorageServiceTests.cs`
- `UploadAsync_PrefixesPhysicalBlobNameWithCallerTenantId`
- `UploadAsync_DifferentTenants_ProduceDifferentPhysicalBlobNames_ForSameLogicalName`
  (the direct non-leakage proof, matching the bar WP-003's doc set)
- `AllBlobNameOperations_NoTenantContext_ReturnFailure_WithoutCallingOperations`
  (fails closed - `BlobStorage.NoTenantContext` - rather than falling back to an
  un-scoped path when there is no authenticated tenant)

## Related

- `docs/WP-003-Tenant-Isolation-Decision.md`, `docs/WP-004-Graph-Multitenancy-Decision.md`
  - same pattern, different layer.
