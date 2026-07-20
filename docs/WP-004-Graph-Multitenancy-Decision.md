# WP-004 — Graph Configuration Is Single-Tenant-Shaped: Decision Required

**Status:** RESOLVED — ruling recorded 2026-07-20. Current single-tenant shape is
accepted as intentional MVP scope, not a defect. The per-tenant redesign is
scheduled as a **hard gate before any second customer's mailbox is connected** -
see `docs/Backlog.md` ("Per-Tenant Graph Configuration").
**Owner:** Chief Technical Architect.
**Raised:** WP-004 delivery.

## Ruling (Chief Technical Architect, 2026-07-20)

Confirmed acceptable for GB Skips as sole customer. Building the N-tenant version
now would be speculative generality against a requirement that doesn't exist yet -
correctly deferred per Engineering Principles ("simplicity first").

- **Redesign ownership:** Chief Technical Architect owns the design; a Backend
  Engineer implements it under a dedicated work package. Tracked in
  `docs/Backlog.md` as **WP-XXX — Per-Tenant Graph Configuration**, status "Not
  Started, Blocking for Customer 2." This is a hard gate on customer #2
  onboarding, not a nice-to-have.
- **Storage shape:** Approved direction is a `TenantGraphConfiguration` table in
  Azure SQL as originally proposed below, keyed by tenant.
- **Auth pattern (correction to this doc's original framing):** Managed Identity
  does **not** solve this problem - it only works within our own Azure tenant and
  cannot authenticate against a customer's separate M365 tenant. The realistic
  long-term pattern is a multi-tenant Entra app registration with per-customer
  admin consent, using either a client secret or certificate per tenant.
  Certificate-based auth is preferred over client secrets where the customer's IT
  will support it, per least-privilege/security standards.
- **Key Vault:** Key Vault can hold arbitrary per-customer secrets; it just needs
  a naming convention (e.g. `graph-secret-{tenantId}`) rather than a single flat
  name. No new secrets platform is required, just a per-tenant naming scheme.
- **Health check:** Agreed it can't remain a single `/health/ready` signal once
  multi-tenant. The exact shape (per-tenant check vs. removal from the shared
  endpoint) is deferred to the same follow-up WP - not solved in isolation here.

The original problem statement and proposed direction below are kept as the
historical record this ruling confirms; treat the "Decision needed" checklist as
answered by the ruling above, not as still-open.

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
