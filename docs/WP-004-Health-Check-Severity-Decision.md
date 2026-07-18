# WP-004 — Graph Health Check Severity: Decision Required

**Status:** OPEN — implemented with a reasoned default; needs explicit sign-off.
**Owner:** Chief Technical Architect.
**Raised:** WP-004 review.
**Update (WP-005):** `BlobStorageHealthCheck` now applies the identical `Degraded`
default and the identical reasoning below, for the same reason (a specific-capability
dependency, not a whole-API dependency). This doc now covers both checks rather than
duplicating a second decision doc for the same judgment call - the decision needed
section below applies to both.

## What exists today

`GraphMailboxHealthCheck` reports `HealthCheckResult.Degraded` (not `Unhealthy`)
when the mailbox is unreachable. ASP.NET Core's default health-check-to-HTTP-status
mapping treats `Degraded` the same as `Healthy` (HTTP 200) - only `Unhealthy` maps
to 503. Both statuses are tagged `"ready"` and visible via `GET /health/ready`'s
response body regardless.

Practical effect: a Graph/mailbox outage does **not** fail `/health/ready`'s HTTP
status the way a database outage does. If that endpoint gates a load balancer or
App Service traffic routing, general API traffic (login, viewing/approving
already-ingested invoices - anything not dependent on email ingestion) keeps
flowing even while Graph is down.

## Why this default was chosen

The database is a genuine hard dependency for nearly every request. Mailbox
connectivity is a dependency of one specific capability (email ingestion), not
of the API as a whole. Treating a Graph outage with the same severity as a
database outage - taking the *entire* API offline over a problem that only
actually affects one feature - seemed like the wrong default to ship silently.

## Why this isn't just implemented and closed

This is a real product/architecture judgment call, not a technical fact - and
this WP's own review process flagged it as needing "explicit sign-off rather
than being an implicit default." It's entirely possible the intended design is
different (e.g. a fully separate `/health/email` endpoint rather than a shared
"ready" tag with mixed severities), and only the Chief Technical Architect can
confirm which is intended.

## Decision needed

- [ ] Confirm `Degraded` (current default) is correct for Graph and Blob Storage, or
      specify a different approach (separate endpoint(s), different tag, something
      else).
- [ ] Confirm whether "Graph down" or "Blob Storage down" should ever page/alert
      differently than "database down" in whatever monitoring consumes these
      endpoints.

## Related

- `docs/WP-003-Tenant-Isolation-Decision.md`, `docs/WP-004-Graph-Multitenancy-Decision.md`
  - same "implement a reasoned default, flag rather than silently decide" pattern.
