# WP-021 — Azure Infrastructure (App Service, SQL, Storage): Decisions Required

**Status:** RESOLVED (2026-07-29, via WP-021a's merge) — both open items below
are closed. See "Resolution (WP-021a)" under each item.
**Owner:** Chief Technical Architect / a human with Entra tenant-creation rights.
**Raised:** WP-021's own source drop (`infra/README.md`), surfaced here per
merge instruction rather than decided during the merge.

## What was merged

New `infra/` folder (Bicep IaC, dev-scope only — `main.bicep` locks
`environmentName` to `'dev'`):

- `infra/main.bicep` — subscription-scope orchestrator (resource group + module call).
- `infra/modules/resources.bicep` — resource-group-scope module: App Service
  Plan + 2 Linux App Services (API, Web), Azure SQL (AAD-only, no SQL
  login/password), Storage Account + Blob container (RBAC only, account keys
  disabled), Key Vault (RBAC model), Log Analytics + Application Insights,
  diagnostic settings on every resource that supports them.
- `infra/scripts/create-entra-app-registrations.sh` — creates the SPA + API
  app registrations, wires the SPA→API scope, grants Graph `Mail.ReadWrite`
  (Application) on the API app, generates its client secret.
- `infra/scripts/grant-sql-managed-identity-access.sql` — creates contained
  DB users for both App Services' managed identities (T-SQL, can't be
  expressed in Bicep/ARM).
- `infra/README.md` — deployment order, validation steps, cost notes.

## Merge-time fix (mechanical, not a design decision)

`main.bicep` line 26 failed to compile: `@description('...subscription''s
own/home...')` used SQL-style `''` escaping for a literal apostrophe, which
Bicep doesn't support (it uses `\'`). This produced `BCP071`/`BCP236` and
blocked `az bicep build` entirely. Fixed to `\'` — no change to the
description text itself, just the escape sequence. Confirmed via `az bicep
build` on both `main.bicep` and `modules/resources.bicep` (clean compile,
zero errors/warnings) after the fix. `modules/resources.bicep` had no issues
of its own.

## Open item 1 — Dev Entra External ID (CIAM) tenant creation

Nothing in this WP's drop can create the tenant `main.bicep`'s Entra
parameters (`entraTenantId` etc.) and the app-registration script depend on.
Creating an Entra External ID (CIAM) tenant requires a human holding
**Tenant Creator** or **Global Administrator** in Entra ID:

- Azure Portal → **Microsoft Entra ID** → **Manage tenants** → **Create** →
  **Microsoft Entra External ID for customers** (CIAM).

Everything else in this WP (the Bicep deployment, the app-registration
script, the SQL grants) is ready to run as soon as this one manual step is
done and its Tenant ID is captured.

- [x] A human with the required Entra role creates the dev CIAM tenant and
      records the Tenant ID.

**Resolution (WP-021a, 2026-07-29):** created and confirmed as tenant type
**External** (a first attempt using a Workforce-type tenant, `tahirayyub`,
was correctly identified and set aside before use). Tenant name
`RameezJav lt.`, domain `rameezjav.onmicrosoft.com`, Tenant ID
`641fc267-7902-48d0-8e1c-1d3d0166c8ac`. See `infra/README.md` for the full
reference table.

## Open item 2 — `Mail.ReadWrite` has no mailbox to target if left in the CIAM tenant

Entra External ID (CIAM) tenants provide **customer/application sign-in**
identity — they do not provision Exchange Online mailboxes. The work
package's own instruction reads as wanting the SPA + API app registrations
*and* the Graph-permissioned app registration in the same new CIAM tenant.
Read literally, the `Mail.ReadWrite` (Application) permission the script
grants would have no mailbox to actually act on.

This is the same class of gap as WP-004's Graph multi-tenancy question
(`docs/WP-004-Graph-Multitenancy-Decision.md`) and the Backlog's tracked
"Per-Tenant Graph Configuration" item (`docs/Backlog.md`) — a structural
mismatch between two different jobs Entra ID does (customer identity vs.
workforce identity/Exchange), not something to guess at.

`create-entra-app-registrations.sh` supports either resolution today via
`--mail-tenant-id` (defaults to the same CIAM tenant with a loud runtime
warning if left unset, so this doesn't block the rest of the WP from being
usable) — but is not "done" until one of these is confirmed:

- [x] Register the Graph-permissioned app in a **separate dev/test
      Microsoft 365 tenant** that actually has an Exchange Online mailbox to
      poll, **or**
- [ ] Defer the Graph/mail piece until GB Skips' own tenant details exist and
      point `--mail-tenant-id` at a stand-in test mailbox tenant in the
      meantime.

**Resolution (WP-021a, 2026-07-29):** Chief Technical Architect ruling —
registered in a separate M365 tenant. The originally-suggested free
Microsoft 365 Developer Program sandbox route was attempted and found
genuinely inaccessible (a 2024 Microsoft policy change restricts it to
qualifying Visual Studio Enterprise/Professional subscribers or Partner
Program members); **Microsoft 365 Business Basic on monthly billing** was
used instead (no eligibility gate, standard commercial signup). Full
provisioning record: `infra/docs/M365-Dev-Mailbox-Tenant.md`. Tenant domain
`acoounts01.onmicrosoft.com`, Tenant ID `1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf`,
mailbox `invoices@acoounts01.onmicrosoft.com`.

**Also ruled on, same merge — app registration count corrected from two to
three.** WP-021's original "interim assumption, not escalated" (API resource
+ Graph client sharing one registration) is overridden: the API resource and
the Graph application-only client are two different security boundaries (one
validates end-user tokens, the other is a no-user-context mailbox credential)
and must not share a registration or a leaked-secret blast radius.
`create-entra-app-registrations.sh` now creates/reuses three registrations —
`APFlow-SPA-Dev`, `APFlow-Api-Dev` (CIAM tenant), and `apflow-graph-dev`
(mail tenant, application-only, `Mail.ReadWrite` + admin consent). See
`infra/README.md` for the full breakdown.

**Also resolved, same merge — `APFlow.Web`'s SQL/Key Vault grants removed.**
The "Observation, not a blocker" below (both App Services getting SQL/Key
Vault access despite `APFlow.Web` having no server-side use for either) was
confirmed as over-provisioning against `02_Project_Standards.md` §4
(least privilege) and removed: `infra/modules/resources.bicep` and
`infra/scripts/grant-sql-managed-identity-access.sql` now grant SQL/Key
Vault access to `APFlow.Api` only. `APFlow.Web`'s Storage grant is
unaffected (not flagged, no server-side storage access pattern was raised).

## WP-021c — Key Vault secret naming correction (2026-07-29)

A wp-021c source drop attempted to rename the live Graph client secret from
`gbskipdev` to `graph-cred-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf`, citing "the
convention ruled on earlier (WP-023 / Backlog per-tenant-readiness item)" as
justification. That citation didn't hold up on inspection: WP-023
("Application Configuration & Secrets (Key Vault)") is still **Not started**
per `README.md`, and the only actual documented convention
(`docs/WP-004-Graph-Multitenancy-Decision.md`, echoed in
`docs/Backlog.md`'s Per-Tenant Graph Configuration item) is
**`graph-secret-{tenantId}`**, not `graph-cred-{tenantId}`. This was
surfaced to the Chief Technical Architect before merging rather than merged
on trust.

**Root cause, confirmed by the Chief Technical Architect:** the
`graph-cred-{tenantId}` naming had appeared in some of the Architect's own
later guidance by mistake — not an engineer error introduced during a
merge, and not a discrepancy in `docs/WP-004-Graph-Multitenancy-Decision.md`
or `docs/Backlog.md` themselves, which were already correct and were left
untouched. The Architect corrected the source guidance directly (see
`infra/docs/M365-Dev-Mailbox-Tenant.md`'s "Graph Client Secret" row, updated
independently of this repo).

**Resolution:** the live Key Vault secret (`kv-apflow-dev-ryd3y6`) was
renamed a second time, to the correct
`graph-secret-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf` — value copied across,
length-verified, old `graph-cred-*` name soft-deleted. wp-021c's own doc
changes (`infra/README.md`, `infra/docs/M365-Dev-Mailbox-Tenant.md`) were
**not merged into this repo** — this repo's docs continue to reference the
secret under its pre-wp-021c name (`gbskipdev`) pending a follow-up drop
that documents the `graph-secret-{tenantId}` name correctly; the live Azure
state is now ahead of the repo's docs on this one field only. Anyone
touching this secret next should use `graph-secret-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf`,
not either prior name.

## Also carried over from the WP's own README (not new decisions, recorded for visibility)

- **Interim assumption, not escalated:** the API app registration doubles as
  both the OAuth2 resource (SPA scope) and the Graph confidential client —
  two app registrations total, not three. Flagged in case a separate Graph
  app registration was actually intended.
- **Observation, not a blocker:** both App Services get Key Vault + SQL
  access (per the WP's literal instruction), even though `APFlow.Web` (the
  static SPA host) has no obvious server-side reason to touch either yet.
  `02_Project_Standards.md` §4 (least privilege) would suggest dropping
  `APFlow.Web`'s SQL/Key Vault grants once confirmed unnecessary — the SQL
  script already grants it `db_datareader` only (not `db_datawriter`) as a
  partial hedge.
- `GET /health/ready` returning `Degraded` in any environment where
  Graph/Blob aren't fully configured yet is expected, not a defect (already
  acknowledged in WP-004's health-check severity decision).

## Related

- `docs/WP-004-Graph-Multitenancy-Decision.md`, `docs/Backlog.md` (Per-Tenant
  Graph Configuration item) — same underlying single-tenant-Graph-shape
  question, different WP.
