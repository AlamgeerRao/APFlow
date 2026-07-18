# WP-003 — Tenant Isolation on Read: Decision Required

**Status:** OPEN — hard gate. Must be resolved before any work package adds a
concrete entity deriving from `TenantEntity` that is queried in a real feature.
**Owner:** Chief Technical Architect.
**Raised:** WP-003 review.

## What exists today

`TenantEntity.TenantId` is stamped automatically on **write** (`AppDbContext`
populates it from the current user's tenant on insert if not already set).

There is **no automatic query filter scoping reads to the current tenant**.
`AppDbContext.OnModelCreating` applies a global soft-delete filter to every
`AuditEntity`-derived type, but not a tenant filter to `TenantEntity`-derived types.
Per Security Standards §4 ("protect tenant isolation at every layer where tenant
data is accessed or stored"), this is a gap on the read side.

Not currently exploitable: there are zero concrete entities deriving from
`TenantEntity` yet ("no invoice entities yet" was in scope for WP-003). This
checklist exists so that stays true only until this decision is made - not
indefinitely.

## Why it wasn't just implemented in WP-003

A global tenant query filter needs to reference per-request state (the current
tenant) inside the EF Core filter expression. EF Core caches the compiled model
once per **DbContext type**, not per instance. A filter that captures the tenant
id as a local variable at model-build time will silently freeze to whichever
tenant happened to create the very first `DbContext` instance in the process,
and silently leak data across every tenant after that. The correct
implementation references a `DbContext` instance field/property directly in the
filter lambda so EF Core re-parameterizes it per instance - this works, but is
easy to get subtly wrong, and could not be validated against a real EF Core
provider in the environment WP-003 was built in (no database, no NuGet access).
Rather than ship an unverified security-critical filter, this was deferred.

## Decision needed

- [ ] Confirm the intended approach: a global `HasQueryFilter` on
      `TenantEntity` (instance-field pattern, as above), a repository-layer
      enforcement point, or something else.
- [ ] Confirm who owns writing and verifying it (ideally with a real EF Core
      provider and a test proving two different tenant contexts against the
      same running process don't leak into each other - the exact failure mode
      described above).
- [ ] Confirm this is implemented and verified **before** the first
      `TenantEntity`-derived entity is queried in a real feature work package.

## Related

- `docs/WP-002-Entra-Verification-Checklist.md` - same "tracked checklist over
  inline comment" pattern, for the Entra role/claim assumptions.
