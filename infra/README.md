# AP Flow — WP-021: Development Infrastructure (Identity + Core Azure Resources)

Scope: **Development environment only.** Nothing here should be pointed at
production. This WP supersedes WP1's infrastructure choices where they
conflict (see "What changed from WP1" below).

---

## STOP / escalation items — ruled on by the Chief Technical Architect, now RESOLVED

**1. Dev Entra External ID (CIAM) tenant creation — DONE.**
Confirmed created and verified as tenant type **External** (the correct type
for CIAM — a first attempt using a **Workforce**-type tenant, `tahirayyub`,
was correctly identified and set aside before use).

| Item | Value |
|---|---|
| Tenant name | `RameezJav lt.` |
| Tenant ID | `641fc267-7902-48d0-8e1c-1d3d0166c8ac` |
| Primary domain | `rameezjav.onmicrosoft.com` |
| Tenant type | External (confirmed) |

**2. Mail.ReadWrite / no mailbox in the CIAM tenant — DONE, route corrected
from original ruling.** The original ruling directed the Graph/mailbox piece
to a free **Microsoft 365 Developer Program** tenant. That route was
attempted and found genuinely inaccessible — a 2024 Microsoft policy change
restricts the free E5 developer sandbox to qualifying Visual Studio
Enterprise/Professional subscribers or Partner Program members. **Microsoft
365 Business Basic, purchased directly on monthly billing, was used instead**
— no eligibility gate, standard commercial signup. Full process recorded in
[`docs/M365-Dev-Mailbox-Tenant.md`](docs/M365-Dev-Mailbox-Tenant.md) as the
reference for standing up an equivalent tenant later.

| Item | Value |
|---|---|
| Tenant domain | `acoounts01.onmicrosoft.com` |
| Tenant ID | `1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf` |
| Mailbox to poll (UPN) | `invoices@acoounts01.onmicrosoft.com` |
| Graph app (created manually) | `apflow-graph-dev`, Client ID `40d63c64-ff18-4028-ba92-01ca93c1c432` |
| `Mail.ReadWrite` (Application) | Granted, admin consent confirmed |
| Client secret | Stored in Key Vault as `graph-secret-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf` (renamed post-deployment from an initial non-conforming `gbskipdev` — see "What changed in wp-021c" below); expires ~1 month from creation (2026-07-29) — acceptable for dev, but **flag for rotation before it lapses; no automated reminder exists** |

`scripts/create-entra-app-registrations.sh` accepts `--graph-client-id` to
reuse this already-created Graph app rather than creating a duplicate — see
"Deployment order" below.

**Correction — three app registrations, not two.** WP-021's original
"interim assumption" (combining the API resource and the Graph
client into one registration to keep the count at two) has been
**overridden by the Chief Technical Architect**: the API resource and the
Graph application-only client are two different security boundaries (one
validates end-user tokens, the other is a no-user-context mailbox
credential) and must not share a registration or a leaked-secret blast
radius. `scripts/create-entra-app-registrations.sh` creates/uses:

1. `APFlow-SPA-Dev` — public client, CIAM tenant (`641fc267-...`).
2. `APFlow-Api-Dev` — resource/scope only, CIAM tenant, no Graph permissions.
3. `apflow-graph-dev` — application-only, Mail.ReadWrite + admin consent +
   client secret — already created manually in the mail-hosting tenant
   (`1df7da13-...`) per the record above; the script reuses it via
   `--graph-client-id` rather than creating a duplicate.

**Resolved — `APFlow.Web`'s SQL/Key Vault grants removed.** Agreed with this
delivery's own "observation for review" flag: a static SPA host has no
server-side logic that would ever need SQL or Key Vault access. Both
`infra/modules/resources.bicep` and
`infra/scripts/grant-sql-managed-identity-access.sql` have been narrowed so
only `APFlow.Api` receives these grants — carrying unused access forward
would have been over-provisioning against `02_Project_Standards.md` §4
(least privilege) for no benefit. `APFlow.Web`'s Storage grant is unaffected
(not flagged, no server-side storage access pattern was raised as a concern).

---

## What's provisioned

| Resource | Purpose | Notes |
|---|---|---|
| Resource Group | `rg-apflow-dev` | |
| App Service Plan (Linux) | Hosts both App Services | `B1` — one plan, shared compute, per WP instruction |
| App Service — APFlow.Api | Backend API | System-assigned identity; `DOTNETCORE\|9.0`; only app with SQL + Key Vault RBAC grants |
| App Service — APFlow.Web | React SPA, served from App Service (confirmed: not Static Web Apps) | System-assigned identity; `NODE\|24-lts` (Active LTS — see "What changed in wp-060" below); Storage access only — no SQL/Key Vault (see ruling above) |
| Azure SQL Server | Azure-AD-only authentication — **no SQL login/password exists** | AAD admin set to a group/user in the subscription's own tenant |
| Azure SQL Database | Single shared database | Confirmed time-boxed position — schema via EF Core migrations only, never in this template |
| Storage Account + Blob Container | Document storage | `allowSharedKeyAccess: false` — no account keys are issued; access is RBAC-only |
| Key Vault | Secret storage | RBAC model; holds only secrets that genuinely need one going forward (e.g. the dedicated Graph app's client secret) |
| Application Insights | App telemetry | Workspace-based (linked to the Log Analytics Workspace below) |
| Log Analytics Workspace | Central log/metric sink | `PerGB2018`, 30-day retention by default |
| Diagnostic settings | On both App Services, the SQL Database, Key Vault, and Blob service | All logs + metrics → Log Analytics |

## Passwordless by design — how "no connection strings or keys in App
Service config directly" is satisfied

- **SQL:** the server is Azure-AD-only (`azureADOnlyAuthentication: true`) —
  there is no SQL password to leak. App settings expose only
  `SQL_SERVER_FQDN` and `SQL_DATABASE_NAME` (both non-secret); `APFlow.Api`
  authenticates as itself via managed identity (`DefaultAzureCredential` /
  `Authentication=Active Directory Managed Identity` in the driver).
  `APFlow.Web` has no SQL access at all (see ruling above).
- **Storage:** `allowSharedKeyAccess: false` — account keys can't be used even
  if someone tried. App settings expose only `STORAGE_ACCOUNT_NAME` and
  `STORAGE_BLOB_ENDPOINT`; both App Services' managed identities hold
  **Storage Blob Data Contributor** via RBAC.
- **Key Vault:** genuinely needed only for secrets that have no
  managed-identity alternative — currently just the dedicated Graph app's
  client secret (Graph app-only auth requires a confidential client
  credential; MI doesn't support the client-credentials/app-only pattern
  against Graph). Only `APFlow.Api` holds **Key Vault Secrets User** via RBAC
  and reads `KEY_VAULT_URI` (non-secret) to resolve secrets at runtime.

## Additional notes from the Chief Technical Architect (acknowledged)

Received alongside this WP, not part of its scope, recorded here for the
permanent record:

- **`GET /health/ready` returning `Degraded` is expected**, not a defect, in
  any environment where Graph/Blob aren't fully configured yet. No alerting
  is configured in this WP (Application Insights alert rules are out of
  scope here), so there's nothing to correct — flagging this so whoever
  builds alerting later starts from the right baseline: only `Unhealthy` on
  the `database` component is a real signal.
- **Secret naming convention is a separate, already-scoped task.** Addressed
  above — `--secret-name` is now a required argument on the Entra script
  rather than a name this WP invents.
- **The `03_Solution_Structure.md` §0 deviation (ADR-SA011-DEV-001) and the
  `06_Domain_Reference_Data.md` status catalogue differences are expected**
  and don't affect this WP's scope. No action taken — this infrastructure
  doesn't encode the .NET solution's project layout or the invoice status
  catalogue, so neither is affected by the deviation or the catalogue's
  current state.

## `APFlow.Web` least-privilege — resolved, see ruling above

Covered under "STOP / escalation items" above — `APFlow.Web`'s SQL and Key
Vault grants have been removed. Not repeated here to avoid two sources of
truth on the same decision.

## What changed in wp-021c (post-deployment Chief Technical Architect review)

Two items raised after WP-021b's actual Azure deployment, both fixed
directly against the live environment (no redeploy needed — docs-only
change here to match):

1. **Key Vault secret naming convention.** The Graph client secret was
   initially stored as `gbskipdev` — not following an established naming
   convention. Renamed directly in Key Vault at the time to
   `graph-cred-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf`, citing "WP-023 /
   Backlog per-tenant-readiness item" as the source of a
   `graph-cred-{tenantId}` convention.
   **This citation and naming were themselves wrong — corrected in
   `wp-021d`, see the section below. Do not use `graph-cred-` for anything;
   read on for the actual correct name and convention.**
2. **Confirmed `APFlow.Web` has no SQL/Key Vault access.** Verified directly
   against the live deployment: `sys.database_principals` shows only the API
   app's managed identity as an external user (none for Web), and the Key
   Vault's role assignments show only the API app (Key Vault Secrets User)
   and the deploying admin (Key Vault Secrets Officer) — no Web app entry.
   The earlier least-privilege fix in `wp-021a`/`resources.bicep` worked as
   intended; nothing further to change.

## What changed in wp-060 (Node.js LTS upgrade — spans WP-021 and WP-022)

Spotted directly in the Azure Portal: `APFlow.Web`'s App Service was flagged
as running an end-of-life runtime stack. Confirmed —
**Node.js 20 reached end-of-life on 2026-04-30**; it had been unsupported
for 3 months at the time this was caught. No further security patches are
issued for it upstream.

**Fix:** bumped to **Node.js 24 — Active LTS** (entered LTS October 2025,
EOL 2028-04-30), the longest-runway supported option currently available as
an Azure App Service Linux runtime stack (`NODE|24-lts`), ahead of the more
conservative Node 22 (Maintenance LTS, EOL 2027-04-30). Changed in:
- `infra/modules/resources.bicep` — `linuxFxVersion` for `APFlow.Web`.
- `.github/workflows/ci-cd.yml` (WP-022) — `NODE_VERSION` for the
  build/lint/test job, so CI exercises the same Node major version that
  actually runs in production rather than a stale one.

**This WP-060 package is also the first correctly-rooted drop of WP-022's
files.** The prior `wp-022b` drop nested everything under an extra `wp-022/`
folder rather than matching real repo paths — QA flagged this before merge.
This package fixes that at the same time: `.github/workflows/ci-cd.yml` and
`docs/CI-CD-Pipeline.md` sit at the repo root as they should, and both
one-time setup scripts (`setup-github-oidc-service-principal.sh`,
`grant-ci-sql-migration-access.sql`) live under `infra/scripts/` alongside
WP-021's own scripts — same category of thing (manually-run, elevated-
privilege Azure/Entra setup), not application code, so grouped with their
siblings rather than a new top-level `scripts/` folder.

**No other resource's runtime stack needed changing** — `APFlow.Api` runs
`DOTNETCORE|9.0`, which isn't affected by this issue.

## What changed in wp-021d (QA-caught naming correction)

QA compared `wp-021c`'s change against the actual decision record and found
it didn't hold up: **`WP-023` ("Application Configuration & Secrets (Key
Vault)") is still Not Started** — it never ruled on anything — and the only
genuinely documented convention (`docs/WP-004-Graph-Multitenancy-Decision.md`,
echoed in `docs/Backlog.md`'s Per-Tenant Graph Configuration item) is
**`graph-secret-{tenantId}`**, not `graph-cred-{tenantId}`.

**Root cause (confirmed by the Chief Technical Architect):** the
`graph-cred-{tenantId}` form had appeared in some of the Architect's own
later guidance by mistake — not an error introduced during any merge, and
`docs/WP-004-Graph-Multitenancy-Decision.md`/`docs/Backlog.md` themselves
were already correct throughout and were never touched. The Architect
corrected the source guidance directly.

**Corrected, everywhere in this README and `docs/M365-Dev-Mailbox-Tenant.md`:**
the Graph client secret's Key Vault name is
`graph-secret-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf` — **not**
`graph-cred-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf`, and **not** the original
`gbskipdev`. **For any future environment, name this secret
`graph-secret-{mail-tenant-id}`.**

The live Key Vault secret in `kv-apflow-dev-ryd3y6` should be renamed a
second time to match (same three-command pattern used for the first rename
— read the existing value, write it under the correct name, delete the
incorrect one):
```bash
SECRET_VALUE=$(az keyvault secret show --vault-name kv-apflow-dev-ryd3y6 --name graph-cred-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf --query value -o tsv)
az keyvault secret set --vault-name kv-apflow-dev-ryd3y6 --name graph-secret-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf --value "$SECRET_VALUE"
az keyvault secret delete --vault-name kv-apflow-dev-ryd3y6 --name graph-cred-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf
unset SECRET_VALUE
```

One incidental finding during verification, not a defect: `az role
assignment list`'s `principalName` column can display a managed identity's
**App ID** rather than its Object ID, which briefly looked like a mismatch
against `az webapp identity show`'s Object ID output. Cross-checking with
`principalId` (not `principalName`) resolved it — worth knowing if anyone
re-verifies this later and sees the same apparent discrepancy.

## What changed in wp-021b (bug found during actual deployment)

`az deployment sub create` against a real subscription (UK West, per the
Chief Technical Architect's data-residency preference — UK South hit a
subscription compute quota limit and was abandoned) surfaced a genuine
template bug: the generated Key Vault name (`kv-apflow-dev-<13-char-suffix>`)
came out to 27 characters, 3 over Azure's hard 24-character limit for vault
names. Fixed by shortening the uniqueness suffix used in that one name from
13 to 6 characters (`kv-apflow-dev-<6-char-suffix>` = 20 chars) — no other
resource name was affected; all were re-checked against their real Azure
length limits and are within bounds (storage account sits exactly at its
24-character limit with the default `apflow`/`dev` naming — flagged as a
code comment for anyone lengthening those defaults later, not currently a
problem).

## What changed from WP1

- **Static Web App → App Service** for the frontend, per this WP's explicit
  confirmation ("Azure App Service for both, not Static Web Apps").
- **SQL: username/password + Key-Vault-stored connection string → Azure-AD-only,
  zero SQL secrets.** More consistent with "never hardcode secrets" /
  least-privilege than WP1's Key-Vault-reference approach, and directly
  matches this WP's "no connection strings or keys in App Service config"
  instruction.
- **Storage: account-key connection string in Key Vault → RBAC-only, keys
  disabled entirely.**
- **Added:** Application Insights, Log Analytics Workspace, diagnostic
  settings on every resource that supports them — none of this existed in WP1.
- **Added:** Entra External ID app registrations (Task 1), which WP1 didn't
  cover at all.

---

## Deployment order

Run these in order — later steps depend on outputs from earlier ones.

### 0. Prerequisites

- Azure CLI, logged in with `Contributor` + `User Access Administrator` (or
  `Owner`) on the target subscription.
- The dev Entra External ID (CIAM) tenant already created (see STOP item 1).
- The Object ID (in the **subscription's own/home Entra tenant** — not the new
  CIAM tenant) of whichever user or group should be the SQL AAD admin, e.g.:
  ```bash
  az ad signed-in-user show --query id -o tsv
  ```

### 1. Deploy the core infrastructure

```bash
az login
az account set --subscription "<subscription-id-or-name>"

az deployment sub create \
  --location ukwest \
  --template-file infra/main.bicep \
  --parameters \
      location="ukwest" \
      sqlAadAdminObjectId="<object-id-from-step-0>" \
      sqlAadAdminLogin="<display-name-of-that-user-or-group>"
```

Note there are **two separate `--location`-type values here, both needed**:
the `az deployment sub create --location` flag just tells Azure where to
store the *deployment record* itself and can technically be any region; the
`location=` **template parameter** is what actually controls where the App
Service Plan, SQL Server, Storage Account, etc. get created — this is the
one that matters for data residency and quota. `uksouth` was tried first for
this environment and hit a subscription compute quota wall
(`SubscriptionIsOverQuotaForSku`); `ukwest` had quota available and keeps
data in the UK.

Capture the outputs — you'll need `apiAppServiceUrl`, `webAppServiceUrl`,
`sqlServerFqdn`, `sqlDatabaseName`, `keyVaultName` for the next steps:

```bash
az deployment sub show --name <deployment-name> --query properties.outputs
```

### 2. Run the Entra App Registration script (Task 1)

Requires both the CIAM tenant (step 0) and an M365 tenant with a real
mailbox already provisioned — see
[`docs/M365-Dev-Mailbox-Tenant.md`](docs/M365-Dev-Mailbox-Tenant.md) for how,
and the confirmed reference values below.

```bash
az login --tenant 641fc267-7902-48d0-8e1c-1d3d0166c8ac --allow-no-subscriptions

./infra/scripts/create-entra-app-registrations.sh \
  --tenant-id 641fc267-7902-48d0-8e1c-1d3d0166c8ac \
  --mail-tenant-id 1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf \
  --graph-client-id 40d63c64-ff18-4028-ba92-01ca93c1c432 \
  --web-app-url <webAppServiceUrl-from-step-1>
```

`--graph-client-id` tells the script this dev environment's Graph app
(`apflow-graph-dev`) already exists — it reuses it instead of creating a
duplicate, and doesn't touch its `Mail.ReadWrite` permission or reset its
secret. Its client secret is stored in Key Vault as
`graph-secret-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf`, per the
`graph-secret-{tenantId}` naming convention (per `docs/WP-004-Graph-Multitenancy-Decision.md`
/ `docs/Backlog.md` — see "What changed in wp-021d" above for why this isn't
`graph-cred-{tenantId}`) (do this yourself, directly from
your terminal — never through this script or any chat channel):
```bash
az keyvault secret set --vault-name <keyVaultName-from-step-1> --name graph-secret-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf --value <the-value-you-already-have>
```

(The `--key-vault-name`/`--secret-name`/`--subscription-tenant-id` flags
below only apply when the script is generating a **new** Graph app secret —
not needed for this environment, since the Graph app already exists. For a
new environment, name the secret `graph-secret-{mail-tenant-id}` to follow the
same convention.)

Capture the printed reference values (SPA client ID, API client ID, API
scope) — see the table below, already partly filled in.

### 3. Re-deploy to wire the Entra values into the API/Web app settings (optional but recommended)

```bash
az deployment sub create \
  --location ukwest \
  --template-file infra/main.bicep \
  --parameters \
      location="ukwest" \
      sqlAadAdminObjectId="<object-id-from-step-0>" \
      sqlAadAdminLogin="<display-name-of-that-user-or-group>" \
      entraTenantId="641fc267-7902-48d0-8e1c-1d3d0166c8ac" \
      entraSpaClientId="<from-step-2>" \
      entraApiClientId="<from-step-2>" \
      entraApiScope="<from-step-2>"
```

Bicep deployments are idempotent — this only updates the app settings that
changed.

### 4. Grant `APFlow.Api`'s managed identity access inside SQL

Connect as the AAD admin from step 0 and run
`scripts/grant-sql-managed-identity-access.sql`, after replacing its
placeholder name with the actual `apiAppServiceName` output from step 1.
(`APFlow.Web` intentionally gets no database user — see the ruling above.)

```bash
sqlcmd -S <sqlServerFqdn> -d <sqlDatabaseName> -G \
  --authentication-method=ActiveDirectoryDefault \
  -i infra/scripts/grant-sql-managed-identity-access.sql
```

---

## Validation steps

```bash
# Resources exist
az resource list -g rg-apflow-dev -o table

# Both App Services are reachable
curl -I <apiAppServiceUrl>
curl -I <webAppServiceUrl>

# No SQL password exists (should show AAD-only, no administratorLogin secret)
az sql server show -g rg-apflow-dev -n <sqlServerName-without-fqdn-suffix> --query administrators

# Storage account keys are disabled
az storage account show -g rg-apflow-dev -n <storageAccountName> --query allowSharedKeyAccess
# -> should print false

# App settings contain no secrets — only non-secret identifiers and (if wired
# up) a single @Microsoft.KeyVault(...) reference for the Graph secret
az webapp config appsettings list -g rg-apflow-dev -n <apiAppServiceName> -o table

# Contained SQL user exists for APFlow.Api's managed identity (APFlow.Web
# intentionally has none — see ruling above)
sqlcmd -S <sqlServerFqdn> -d <sqlDatabaseName> -G --authentication-method=ActiveDirectoryDefault \
  -Q "SELECT name, type_desc FROM sys.database_principals WHERE type = 'E'"

# App Insights is receiving telemetry (after some app traffic)
az monitor app-insights component show -g rg-apflow-dev -a <appInsightsName>

# Diagnostic settings are attached
az monitor diagnostic-settings list --resource <apiAppService-resource-id>
```

---

## Reference values for the Backend and React Engineers

Fill remaining blanks in from Step 1's App Service URLs and Step 2's SPA/API
script output and hand it to whoever picks up the CORS task and the SPA/API
auth wiring. No secrets are included here — only the values needed to
configure MSAL/OIDC on each side. Tenant and Graph values are already
confirmed (see STOP items above / `docs/M365-Dev-Mailbox-Tenant.md`).

| Value | Where it's used | Value |
|---|---|---|
| API App Service URL | Backend CORS config (separate task); browser calls | *(from Step 1 deployment output — not yet run)* |
| Web App Service URL | Backend CORS config; SPA redirect URI | *(from Step 1 deployment output — not yet run)* |
| Entra tenant ID (sign-in, CIAM) | Both SPA and API auth config | `641fc267-7902-48d0-8e1c-1d3d0166c8ac` |
| SPA client ID | React MSAL config | *(from Task 1 script output — not yet run)* |
| SPA redirect URIs | React MSAL config | `http://localhost:5173`, `<Web App Service URL>` |
| API client ID / audience | Backend JWT bearer validation config | *(from Task 1 script output — not yet run)* |
| API scope | React MSAL — scope requested when calling the API | `api://<API client ID>/access_as_user` |
| Graph/mail tenant ID | Workers' Graph client-credentials config | `1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf` |
| Graph client ID | Workers' Graph client-credentials config (separate from the API client ID) | `40d63c64-ff18-4028-ba92-01ca93c1c432` |
| Graph client secret name (Key Vault) | Workers reads this from Key Vault at runtime — never hardcoded | `graph-secret-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf` (expires ~2026-08-29 — rotate before then) |
| Test mailbox UPN | What Workers actually polls in dev | `invoices@acoounts01.onmicrosoft.com` |

---

## Cost notes (dev defaults, `ukwest`, approximate)

- App Service Plan `B1` (shared by both apps): ~£10/month
- SQL Database `Basic`: ~£4/month
- Storage Account (LRS, low volume): a few pence to low pounds/month
- Key Vault: negligible at dev volume
- Log Analytics + Application Insights: pay-as-you-go on ingested GB — at dev
  traffic levels, low single-digit £/month; watch this as usage grows since
  it's the one line item that scales with activity rather than being flat

Roughly **£20–25/month** all-in for dev.

## What's intentionally NOT in this WP

- GitHub/GitHub Actions wiring (separate WP)
- CORS configuration on the API (explicitly scoped elsewhere — this WP only
  supplies the two URLs it needs)
- The real GB Skips Entra tenant (Task 1 is a dev-only stand-in tenant)
- VNet integration, Private Endpoints, deployment slots — no concrete
  requirement for these yet
