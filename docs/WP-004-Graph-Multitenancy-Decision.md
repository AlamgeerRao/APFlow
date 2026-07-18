# WP-004 — Graph Configuration Is Single-Tenant-Shaped: Decision Required

**Status:** OPEN — not a hard gate for the initial customer, but must be resolved
before a second customer with their own mailbox/tenant is onboarded.
**Owner:** Chief Technical Architect.
**Raised:** WP-004 delivery.

## What exists today

`GraphOptions` is a single, app-wide configuration (one `TenantId`, one `ClientId`,
one `MailboxUserPrincipalName`) bound once at startup and used for every request,
regardless of which AP Flow tenant/customer is making it.

## Why this is a problem for the stated architecture

AP Flow is explicitly "multi-tenant by design" (01_Project_Context.md), with GB
Skips as the *initial* customer, not the only one. Each customer will have their
own Microsoft 365/Entra ID tenant, their own AP mailbox, and (most likely) their
own Graph App Registration with its own admin-consented permissions. A single
global `GraphOptions` cannot represent "customer B's mailbox" once customer B
exists - the current shape assumes exactly one mailbox for the whole application.

This is not an oversight so much as a deliberate MVP-scope choice: WP-004's task
list matches a single-mailbox configuration, and GB Skips is currently the only
customer. But it means the current implementation will not scale to the second
tenant without a real design change, not just a config addition.

## What would need to change

Likely direction (not decided - this needs explicit sign-off, not silent
implementation):

- Move Graph configuration from `appsettings`/Key Vault (app-wide) to **per-tenant
  data**, most likely a `TenantGraphConfiguration`-shaped table in Azure SQL,
  associated with each tenant record.
- Client secrets, if any tenant uses that path rather than Managed Identity,
  would need per-tenant encryption at rest (Key Vault alone can't hold N
  arbitrary per-customer secrets the same way it holds one app-wide secret -
  needs a real secrets-per-tenant strategy).
- `EmailService`/`GraphServiceClient` construction would need to become
  per-tenant (e.g. resolved per-request from `ICurrentUserService.TenantId`)
  rather than a single DI singleton built once at startup.
- The health check (`GraphMailboxHealthCheck`) would need to become per-tenant
  or be reconsidered entirely (a single "/health/ready" can't meaningfully
  represent N different tenants' mailbox connectivity).

## Decision needed

- [ ] Confirm this is acceptable MVP scope for GB Skips as the sole customer.
- [ ] Confirm who owns the per-tenant redesign, and roughly when (before or as
      part of onboarding the second customer).
- [ ] Confirm whether Managed Identity or client-secret-based auth is the
      intended long-term pattern per customer, since that affects the secrets
      strategy above.

## Related

- `docs/WP-003-Tenant-Isolation-Decision.md` - same "flag now, don't silently
  build the wrong shape" pattern, for query-level tenant isolation.
