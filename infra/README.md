# AP Flow — WP-021: Development Infrastructure (Identity + Core Azure Resources)

Scope: **Development environment only.** Nothing here should be pointed at
production. This WP supersedes WP1's infrastructure choices where they
conflict (see "What changed from WP1" below).

---

## STOP / escalation items (read first)

Per Task 1's own instruction ("if you don't hold sufficient Azure AD
privileges to create the tenant itself, escalate that specific step rather
than blocking the rest of this WP on it"), two items are raised rather than
resolved by assumption:

**1. Dev Entra External ID (CIAM) tenant creation — escalated, not blocking.**
Creating a new Entra tenant requires a human with the **Tenant Creator** (or
**Global Administrator**) role — this agent has no Azure AD privileges to
create one. Everything else in this WP (Bicep infra, the App Registration
script, SQL grants) is ready to run as soon as a human completes this one
manual step:

- Azure Portal → **Microsoft Entra ID** → **Manage tenants** → **Create** →
  **Microsoft Entra External ID for customers** (CIAM).
- Record the resulting **Tenant ID** — it's the first input every other
  script in this WP needs.

**2. Mail.ReadWrite has no mailbox to target if left in the CIAM tenant —
needs a Chief Technical Architect decision.** Entra External ID (CIAM)
tenants provide *customer/application sign-in* identity; they do not
provision Exchange Online mailboxes. The work package asks for the SPA + API
app registrations to live in the new CIAM tenant *and* for "the Graph App
Registration" to carry Mail.ReadWrite (Application) + admin consent. Read
literally as the same tenant, that permission would have nothing to read —
there's no mailbox in a CIAM tenant to grant access to.

This is not the same kind of gap as a missing role or status (§ of
`06_Domain_Reference_Data.md`) — it's a structural mismatch between two
different jobs Entra ID does (customer identity vs. workforce
identity/Exchange), so it's raised here rather than guessed at. Two
resolutions are consistent with WP-004's prior ruling (single-tenant Graph
shape accepted as intentional MVP scope; per-tenant redesign gated on
customer #2):

- Register the Graph-permissioned app in a **separate dev/test Microsoft 365
  tenant** that actually has an Exchange Online mailbox to poll, or
- Defer the Graph/mail piece until GB Skips' own tenant details are available
  and use a stand-in test mailbox in whatever tenant already exists for that
  purpose.

`scripts/create-entra-app-registrations.sh` supports either path via
`--mail-tenant-id` (defaults to the same CIAM tenant with a loud warning if
left unset) — implemented so this doesn't block the rest of the WP, but not
treated as "done" until the Chief Technical Architect confirms which tenant
should actually hold this permission.

**Interim assumption made without escalation (documented, not a blocker):**
the API app registration doubles as both the OAuth2 resource (SPA scope) and
the Graph confidential client, keeping the total at exactly two app
registrations as specified, rather than introducing an unstated third one.
Flagging this so it's easy to correct if a separate Graph app registration
was actually intended.

---

## What's provisioned

| Resource | Purpose | Notes |
|---|---|---|
| Resource Group | `rg-apflow-dev` | |
| App Service Plan (Linux) | Hosts both App Services | `B1` — one plan, shared compute, per WP instruction |
| App Service — APFlow.Api | Backend API | System-assigned identity; `DOTNETCORE\|9.0` |
| App Service — APFlow.Web | React SPA, served from App Service (confirmed: not Static Web Apps) | System-assigned identity; `NODE\|20-lts`, needs a small static-file server in the app itself (Frontend Engineer concern, out of scope here) |
| Azure SQL Server | Azure-AD-only authentication — **no SQL login/password exists** | AAD admin set to a group/user in the subscription's own tenant |
| Azure SQL Database | Single shared database | Confirmed time-boxed position — schema via EF Core migrations only, never in this template |
| Storage Account + Blob Container | Document storage | `allowSharedKeyAccess: false` — no account keys are issued; access is RBAC-only |
| Key Vault | Secret storage | RBAC model; holds only secrets that genuinely need one going forward (e.g. the Graph client secret) |
| Application Insights | App telemetry | Workspace-based (linked to the Log Analytics Workspace below) |
| Log Analytics Workspace | Central log/metric sink | `PerGB2018`, 30-day retention by default |
| Diagnostic settings | On both App Services, the SQL Database, Key Vault, and Blob service | All logs + metrics → Log Analytics |

## Passwordless by design — how "no connection strings or keys in App
Service config directly" is satisfied

- **SQL:** the server is Azure-AD-only (`azureADOnlyAuthentication: true`) —
  there is no SQL password to leak. App settings expose only
  `SQL_SERVER_FQDN` and `SQL_DATABASE_NAME` (both non-secret); the app
  authenticates as itself via managed identity (`DefaultAzureCredential` /
  `Authentication=Active Directory Managed Identity` in the driver).
- **Storage:** `allowSharedKeyAccess: false` — account keys can't be used even
  if someone tried. App settings expose only `STORAGE_ACCOUNT_NAME` and
  `STORAGE_BLOB_ENDPOINT`; both App Services' managed identities hold
  **Storage Blob Data Contributor** via RBAC.
- **Key Vault:** genuinely needed only for secrets that have no
  managed-identity alternative — currently just the Microsoft Graph client
  secret (Graph app-only auth requires a confidential client credential; MI
  doesn't support the client-credentials/app-only pattern against Graph).
  Both App Services hold **Key Vault Secrets User** via RBAC and read
  `KEY_VAULT_URI` (non-secret) to resolve secrets at runtime.

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

## Observation for review (not a blocker)

The work package asks for both App Services to get Key Vault and SQL access,
and this template does that literally. In practice, `APFlow.Web` serves
static SPA assets and has no obvious server-side reason to touch SQL or Key
Vault yet. This has been implemented as instructed rather than narrowed
unilaterally, but is flagged for the Chief Technical Architect to confirm
during review — least privilege (`02_Project_Standards.md` §4) would suggest
dropping `APFlow.Web`'s SQL/Key Vault grants once it's confirmed the SPA host
never needs them.

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
  --location uksouth \
  --template-file infra/main.bicep \
  --parameters \
      sqlAadAdminObjectId="<object-id-from-step-0>" \
      sqlAadAdminLogin="<display-name-of-that-user-or-group>"
```

Capture the outputs — you'll need `apiAppServiceUrl`, `webAppServiceUrl`,
`sqlServerFqdn`, `sqlDatabaseName`, `keyVaultName` for the next steps:

```bash
az deployment sub show --name <deployment-name> --query properties.outputs
```

### 2. Run the Entra App Registration script (Task 1)

```bash
az login --tenant <ciam-tenant-id> --allow-no-subscriptions

./infra/scripts/create-entra-app-registrations.sh \
  --tenant-id <ciam-tenant-id> \
  --web-app-url <webAppServiceUrl-from-step-1> \
  --key-vault-name <keyVaultName-from-step-1> \
  --secret-name <name-from-the-separate-secret-naming-convention-task>
  # add --mail-tenant-id <...> once the Chief Technical Architect confirms
  # which tenant should host the Mail.ReadWrite permission (see STOP item 2)
```

The secret's *name* (e.g. `graph-cred-{tenantId}`) is a separate, already-scoped
task — this script only provisions the value. `--secret-name` is required
whenever `--key-vault-name` is given; omit both to just have the script print
the secret instead of storing it.

Capture the printed reference values (tenant ID, SPA client ID, API client
ID, API scope) — see the table template below.

### 3. Re-deploy to wire the Entra values into the API/Web app settings (optional but recommended)

```bash
az deployment sub create \
  --location uksouth \
  --template-file infra/main.bicep \
  --parameters \
      sqlAadAdminObjectId="<object-id-from-step-0>" \
      sqlAadAdminLogin="<display-name-of-that-user-or-group>" \
      entraTenantId="<from-step-2>" \
      entraSpaClientId="<from-step-2>" \
      entraApiClientId="<from-step-2>" \
      entraApiScope="<from-step-2>"
```

Bicep deployments are idempotent — this only updates the app settings that
changed.

### 4. Grant the App Services' managed identities access inside SQL

Connect as the AAD admin from step 0 and run
`scripts/grant-sql-managed-identity-access.sql`, after replacing its two
placeholder names with the actual `apiAppServiceName` / `webAppServiceName`
outputs from step 1:

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

# Contained SQL users exist for both managed identities
# (run inside the .sql script's own SELECT verification query, or):
sqlcmd -S <sqlServerFqdn> -d <sqlDatabaseName> -G --authentication-method=ActiveDirectoryDefault \
  -Q "SELECT name, type_desc FROM sys.database_principals WHERE type = 'E'"

# App Insights is receiving telemetry (after some app traffic)
az monitor app-insights component show -g rg-apflow-dev -a <appInsightsName>

# Diagnostic settings are attached
az monitor diagnostic-settings list --resource <apiAppService-resource-id>
```

---

## Reference values for the Backend and React Engineers

Fill this in from Step 1 and Step 2's outputs and hand it to whoever picks up
the CORS task and the SPA/API auth wiring. No secrets are included here —
only the values needed to configure MSAL/OIDC on each side.

| Value | Where it's used | Fill in after deployment |
|---|---|---|
| API App Service URL | Backend CORS config (separate task); browser calls | `https://app-apflow-dev-api-<suffix>.azurewebsites.net` |
| Web App Service URL | Backend CORS config; SPA redirect URI | `https://app-apflow-dev-web-<suffix>.azurewebsites.net` |
| Entra tenant ID (sign-in) | Both SPA and API auth config | *(from Task 1 script output)* |
| SPA client ID | React MSAL config | *(from Task 1 script output)* |
| SPA redirect URIs | React MSAL config | `http://localhost:5173`, `<Web App Service URL>` |
| API client ID / audience | Backend JWT bearer validation config | *(from Task 1 script output)* |
| API scope | React MSAL — scope requested when calling the API | `api://<API client ID>/access_as_user` |
| Graph/mail tenant | Only relevant to Workers' Graph client-credentials config | *(pending Chief Technical Architect decision — STOP item 2)* |

---

## Cost notes (dev defaults, `uksouth`, approximate)

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
