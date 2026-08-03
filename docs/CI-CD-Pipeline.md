# AP Flow — WP-022: CI/CD Pipeline (Development)

<!-- WP-078 path-filter test edit, 2026-08-03: docs-only change, pushed on
its own to confirm detect-changes sets backend-changed/frontend-changed
both false and every build/test/deploy job is skipped. Safe to remove
after the test run is confirmed. -->

Scope: **Development environment only**, deploying to the two App Services
provisioned in WP-021:

```
API:  https://app-apflow-dev-api-ryd3y6fyfloxu.azurewebsites.net
Web:  https://app-apflow-dev-web-ryd3y6fyfloxu.azurewebsites.net
```

**wp-060 note:** this drop corrects two things QA flagged/caught after the
original `wp-022b` drop, both reflected below and in the file paths
themselves:
1. **File structure** — the original drop nested everything under an extra
   `wp-022/` folder rather than matching real repo paths. Corrected here:
   `.github/workflows/` and `docs/` sit at the repo root; both one-time
   setup scripts now live under `infra/scripts/` alongside WP-021's own
   scripts (same category of thing — manual, elevated-privilege Azure/Entra
   setup — not a new top-level `scripts/` folder).
2. **Node.js version** — `APFlow.Web` was flagged in the Azure Portal as
   running Node 20, which reached end-of-life 2026-04-30. Bumped to Node 24
   (Active LTS) in both the deployed runtime stack (WP-021's
   `resources.bicep`) and this pipeline's own build/test step, so CI
   exercises the same Node major version that actually runs in production.

**Post-wp-060 note (2026-07-31): two real bugs found on the first live pipeline
run, both fixed here:**
1. `dotnet ef database update` failed with `APFlow.Api doesn't reference
   Microsoft.EntityFrameworkCore.Design` — `Infrastructure`'s existing
   reference is `PrivateAssets="all"`, which deliberately keeps it out of
   `APFlow.Api`'s graph, but EF's tools load design-time services from the
   **startup project** (`APFlow.Api`), not `--project`. Fixed by adding the
   same (also-private) reference to `APFlow.Api.csproj`.
2. The next run failed with SQL error 40 (`Could not open a connection to
   SQL Server`) — GitHub-hosted runners have no static IP and are not
   covered by the SQL server's "Allow Azure services" firewall rule (that
   rule only covers Azure's own trusted PaaS-to-PaaS traffic). Fixed by
   adding "Add/Remove temporary SQL firewall rule" steps around the
   migration in `ci-cd.yml`, scoped to exactly that run's IP and removed
   unconditionally afterward. **This requires a new `RESOURCE_GROUP`
   variable and a new `SQL Server Contributor` RBAC grant** — see the
   updated tables below and the one-time setup section.

**Also found while investigating (2): the one-time setup script had likely
never actually been executed — resolved 2026-07-31.** As tenant Global
Administrator, only `APFlow-SPA-Dev` and `APFlow-Api-Dev` existed as app
registrations in the CIAM tenant — no `APFlow-CI-Dev`. Separately, as
subscription Owner, zero RBAC role assignments existed on `rg-apflow-dev` at
all, meaning `Website Contributor` had never been granted either. **The user
then ran the setup script for real** (with the new `--sql-server-name`
argument): `APFlow-CI-Dev` created (Client ID
`c49371f6-7723-42a7-b9e0-ae0e39b4bf1b`, no client secret, OIDC federated
credential scoped to `repo:AlamgeerRao/APFlow:ref:refs/heads/main`);
`Website Contributor` granted on `rg-apflow-dev`; `SQL Server Contributor`
granted on `sql-apflow-dev-ryd3y6fyfloxu`; `grant-ci-sql-migration-access.sql`
run via the Portal Query Editor (confirmed `APFlow-CI-Dev` present in
`sys.database_principals` with migration rights); `CI_AZURE_CLIENT_ID` /
`AZURE_TENANT_ID` / `AZURE_SUBSCRIPTION_ID` set as GitHub Environment
variables. Full detail in `docs/Backlog.md`'s Closed section.

`RESOURCE_GROUP` (`rg-apflow-dev`, needed for bug (2)'s fix) confirmed set
as a GitHub Environment variable too, 2026-07-31. **All setup steps and
both bug fixes are now in place; none of it has been exercised by a real
pipeline run yet** — the next push/`workflow_dispatch` run is the actual
end-to-end verification, not the setup completion itself.

**Post-wp-078 note (2026-08-03): path-based filtering added.** A new
`detect-changes` job (`dorny/paths-filter@v4`) runs first and gates every
downstream job on which side of the codebase the push/PR actually touched —
the 5 `backend-build-test` matrix legs, `backend-publish`,
`migrate-development-database`, and `deploy-api` only run when
`backend-changed` is `true` (the six `src/APFlow.*` backend project
directories); `frontend-build-test` and `deploy-web` only run when
`frontend-changed` is `true` (`src/APFlow.Web/**`). `docs/**` and
`README.md` are recognized as doc-only — a change confined to those paths
sets **both** flags `false`, skipping every downstream job (no build, test,
or deploy needed for a docs-only change). Anything else outside all three
recognized sets (this workflow file itself, `infra/**`, `tests/**`, other
root files) or a `workflow_dispatch` manual trigger forces both flags
`true` — deliberately narrow filters, wide safe default, so an ambiguous
change never silently skips something that might matter. Found and fixed
while building this: a *skipped* `needs:` dependency still satisfies
GitHub Actions' default `success()` check, so `migrate-development-database`/
`deploy-api`/`deploy-web` each needed their own explicit
`needs.detect-changes.outputs.*` check — inheriting the skip through
`needs:` alone was not enough to actually stop them running on an
irrelevant change.

---

## Files created

| File | Purpose | Repo location |
|---|---|---|
| `ci-cd.yml` | The pipeline itself | `.github/workflows/ci-cd.yml` |
| `setup-github-oidc-service-principal.sh` | One-time setup: creates the CI/CD Entra app registration + OIDC federation + RBAC | `infra/scripts/setup-github-oidc-service-principal.sh` |
| `grant-ci-sql-migration-access.sql` | One-time setup: grants that identity a database user with migration rights | `infra/scripts/grant-ci-sql-migration-access.sql` |
| `CI-CD-Pipeline.md` | This document | `docs/CI-CD-Pipeline.md` |

---

## Corrections to how Task 8 was read — please confirm

Two things in the work package's wording don't quite match what actually
exists, so rather than force-fitting them, here's what was built instead —
flag if a different intent was meant.

**1. "Entra dev tenant's client IDs/secrets (CIAM tenant, STOP 1)" — there
are no secrets to give.** By design (and correctly, for OAuth security):
- `APFlow-SPA-Dev` is a **public client** — public clients never have a
  secret; that's what makes them safe to ship inside a browser bundle.
- `APFlow-Api-Dev` is a **resource/scope definition only** — it was never
  given a secret either, because nothing in WP-021 needed it to authenticate
  *as* a client anywhere.

So "documenting required secrets" for these two is really "documenting their
Client IDs, which are safe to treat as non-sensitive **GitHub Variables**,
not Secrets" — see the table below. If a future WP needs the API app to act
as a confidential client for something new, that would be the point to
actually generate one.

**2. GitHub Secrets vs. Variables, generally.** Following this project's
passwordless-by-design pattern (AAD-only SQL, keyless Storage, RBAC-only Key
Vault from WP-021), this pipeline needs **zero stored credentials** at all:
- Azure login uses OIDC federation (no client secret — see below).
- SQL access uses "Active Directory Default" authentication (no password —
  SQL has never had one).
- The Graph client secret is never touched by CI/CD; it's read directly from
  Key Vault by the running App Service at runtime, exactly as WP-021 already
  wired up.

So the "GitHub Secrets" list below is genuinely short — almost everything
requested is non-sensitive and belongs in GitHub **Variables** instead.

---

## GitHub configuration required

### Repository/Environment Variables (non-sensitive — `vars.*` in the workflow)

Create a GitHub **Environment** named `development` (Settings → Environments
→ New environment) and add these as **Environment variables** on it (not
repository-wide, since nothing here should ever apply outside Development):

| Variable | Value | Source |
|---|---|---|
| `AZURE_TENANT_ID` | `641fc267-7902-48d0-8e1c-1d3d0166c8ac` | WP-021 (CIAM tenant = subscription's own tenant) |
| `AZURE_SUBSCRIPTION_ID` | `ca6d83dc-24be-412f-a6f4-97da7a4abf5d` | WP-021 |
| `CI_AZURE_CLIENT_ID` | *(output of `setup-github-oidc-service-principal.sh`)* | This WP |
| `API_APP_SERVICE_NAME` | `app-apflow-dev-api-ryd3y6fyfloxu` | WP-021 |
| `WEB_APP_SERVICE_NAME` | `app-apflow-dev-web-ryd3y6fyfloxu` | WP-021 |
| `SQL_SERVER_FQDN` | `sql-apflow-dev-ryd3y6fyfloxu.database.windows.net` | WP-021 |
| `SQL_DATABASE_NAME` | `sqldb-apflow-dev` | WP-021 |
| `RESOURCE_GROUP` | `rg-apflow-dev` | Added post-wp-060 — needed by the migration job's temporary SQL firewall-rule steps (`az sql server firewall-rule create`/`delete` require `--resource-group`) |
| `KEY_VAULT_NAME` | `kv-apflow-dev-ryd3y6` | WP-021 |
| `STORAGE_ACCOUNT_NAME` | `stapflowdevryd3y6fyfloxu` | WP-021 |
| `STORAGE_BLOB_ENDPOINT` | `https://stapflowdevryd3y6fyfloxu.blob.core.windows.net/` | WP-021 |
| `ENTRA_SPA_CLIENT_ID` | `d47fcb44-752e-4d7a-ac49-d3c71dfca7e0` | WP-021 |
| `ENTRA_API_CLIENT_ID` | `603682ec-46ab-4075-9e87-8e44478a39a4` | WP-021 |
| `ENTRA_API_SCOPE` | `api://603682ec-46ab-4075-9e87-8e44478a39a4/access_as_user` | WP-021 |
| `ENTRA_AUTHORITY` | `https://rameezjav.ciamlogin.com/641fc267-7902-48d0-8e1c-1d3d0166c8ac` | **New, added post-wp-022(c)** — the CIAM authority URL `msalConfig.ts` requires (`VITE_ENTRA_AUTHORITY`). Live-verified by querying `https://rameezjav.ciamlogin.com/641fc267-7902-48d0-8e1c-1d3d0166c8ac/v2.0/.well-known/openid-configuration` directly (returned 200 with a matching tenant ID in its endpoints) — not previously documented anywhere in this repo before this fix. |
| `GRAPH_TENANT_ID` | `1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf` | WP-021 |
| `GRAPH_CLIENT_ID` | `40d63c64-ff18-4028-ba92-01ca93c1c432` | WP-021 |
| `GRAPH_MAILBOX_UPN` | `invoices@acoounts01.onmicrosoft.com` | WP-021 |
| `GRAPH_CLIENT_SECRET_KEYVAULT_NAME` | `graph-secret-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf` | WP-021d — a **name**, not the secret itself; the app reads the actual value from Key Vault at runtime. (Corrected from an earlier `wp-021c` naming mistake, caught by QA — `graph-cred-` never was a real convention; see WP-021's README "What changed in wp-021d".) |

The current workflow actually *uses* `AZURE_TENANT_ID`,
`AZURE_SUBSCRIPTION_ID`, `CI_AZURE_CLIENT_ID`, `API_APP_SERVICE_NAME`,
`WEB_APP_SERVICE_NAME`, `SQL_SERVER_FQDN`, `SQL_DATABASE_NAME`, (post-wp-060)
`RESOURCE_GROUP`, and (post-wp-022(c)) `ENTRA_SPA_CLIENT_ID`,
`ENTRA_AUTHORITY`, and `ENTRA_API_SCOPE` (fed into `frontend-build-test`'s
`Build production bundle` step as `VITE_ENTRA_CLIENT_ID`/
`VITE_ENTRA_AUTHORITY`/`VITE_API_SCOPE` — these are compiled into the SPA
bundle at build time, so they must be set correctly *before* that step
runs, not just as App Service runtime settings) —
the rest are documented here for completeness/handoff (per Task 8's ask) and
because the Entra/Graph/Storage values are things the Backend and React
Engineers will need to configure their own code against, even though the
pipeline itself doesn't reference them.

### Frontend build-time variables (`VITE_*`)

Four values are **compiled directly into the `APFlow.Web` JS bundle by
`vite build`** — not read at runtime the way the API's `appSettings` are.
There is no later point at which they can be corrected; getting one wrong
or missing means rebuilding and redeploying, not just changing an Azure
setting. `frontend-build-test`'s `Build production bundle` step sets all
four as `env:` before running `npm run build`, and fails loudly
(`::error::` + `exit 1`) if any is empty:

| `VITE_*` build var | Sourced from (GitHub Environment variable) |
|---|---|
| `VITE_ENTRA_CLIENT_ID` | `ENTRA_SPA_CLIENT_ID` |
| `VITE_ENTRA_AUTHORITY` | `ENTRA_AUTHORITY` |
| `VITE_API_SCOPE` | `ENTRA_API_SCOPE` |
| `VITE_API_BASE_URL` | constructed as `https://${{ vars.API_APP_SERVICE_NAME }}.azurewebsites.net` (not stored as its own variable, so it can't drift from `API_APP_SERVICE_NAME`) |

`VITE_ENTRA_REDIRECT_URI` (see `src/APFlow.Web/.env.example`) is
deliberately **not** set here — it defaults to the page's own origin at
runtime, which already matches the redirect URI registered on the SPA app
registration.

**If you ever add a new required `VITE_*` variable to the frontend**
(`src/APFlow.Web/src/auth/msalConfig.ts`, `httpClient.ts`, or elsewhere),
updating `src/APFlow.Web/.env.example` is not sufficient for a real
deploy to keep working — `.env.example`/`.env.local` only cover local dev.
The new variable also needs adding to this `Build production bundle`
step's `env:` block, sourced from a GitHub Environment variable, or the
production bundle will silently ship without it and fail at runtime in the
browser instead of at build time in CI.

This was found the hard way, 2026-07-31: `ci-cd.yml` ran `npm run build`
with none of these four set for the pipeline's entire existence up to that
point, which only surfaced once a real deploy finally got far enough to
load the actual app (`MSAL initialization failed: Missing required
environment variable: VITE_ENTRA_CLIENT_ID`) instead of Azure's static-site
placeholder — see `docs/Backlog.md`'s Closed section for the full
diagnosis, including a follow-on bug where `frontend-build-test` initially
couldn't see any of these GitHub Environment variables at all because the
job didn't declare `environment: name: development` (GitHub Environment
variables are only visible to a job that references that environment by
name — confirmed this is the *only* job in this workflow that was missing
it; `backend-build-test`/`backend-publish` reference no `vars.*` at all, so
they never needed it, and `migrate-development-database`/`deploy-api`/
`deploy-web` all already declared it correctly for their own Azure-login
`vars.*` reads).

### Repository/Environment Secrets (sensitive)

**None required.** This is intentional, not incomplete — see the correction
above. If a future change reintroduces a stored credential (e.g. a
third-party API key with no OIDC/managed-identity option), add it here as an
actual GitHub *Secret*, scoped to the `development` environment.

---

## One-time setup (run before the first pipeline run)

**Completed 2026-07-31**, including `RESOURCE_GROUP` (see the post-wp-060
note above and `docs/Backlog.md`'s Closed section for the actual values).
Steps 1–3 below are done; step 4 (an actual pipeline run) is the remaining
verification.

**Also found on the first post-setup real run, resolved same day
(2026-07-31): the federated credential's subject shape was wrong.**
`AADSTS700213: No matching federated identity record found for presented
assertion subject
'repo:AlamgeerRao@105811261/APFlow@1302101224:environment:development'`.
Root cause: `migrate-development-database`/`deploy-api`/`deploy-web` all
specify `environment: name: development` — a job referencing a GitHub
Environment gets an **environment-scoped** OIDC subject
(`repo:{org}/{repo}:environment:{name}`), which takes priority over the
**ref-scoped** one (`repo:{org}/{repo}:ref:refs/heads/{branch}`) the setup
script originally created — it does not get both. This account also
includes the numeric owner/repo IDs in the subject
(`{org}@{ownerId}/{repo}@{repoId}`), confirmed directly from the error.
`setup-github-oidc-service-principal.sh` now creates three federated
credentials (ref-based, environment-based, and an id-qualified
environment-based one, to cover both subject shapes) instead of one for any
future setup. **The user added the two missing credentials directly to the
existing `APFlow-CI-Dev` app — confirmed created:**
```bash
az ad app federated-credential create \
  --id 744112fe-2263-4194-adb3-bba7af331a1b \
  --parameters '{
    "name": "github-actions-env-development",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:AlamgeerRao/APFlow:environment:development",
    "audiences": ["api://AzureADTokenExchange"]
  }'

az ad app federated-credential create \
  --id 744112fe-2263-4194-adb3-bba7af331a1b \
  --parameters '{
    "name": "github-actions-env-development-idscoped",
    "issuer": "https://token.actions.githubusercontent.com",
    "subject": "repo:AlamgeerRao@105811261/APFlow@1302101224:environment:development",
    "audiences": ["api://AzureADTokenExchange"]
  }'
```

**Also found on a later real run (2026-07-31): `SQL_SERVER_FQDN` was not
actually set on the `development` Environment**, despite being listed in the
Variables table above as a WP-021-sourced value — the table records what
*should* be set, not confirmation that it was ever entered into the GitHub
UI (the same gap seen once already with `RESOURCE_GROUP`). This produced a
confusing downstream symptom: the firewall-rule step's resource ID built
with an empty server-name segment, which Azure's REST API rejected as
`AuthorizationFailed` on a URL where the firewall rule name appeared to have
shifted into the resource-type position — not an RBAC or Azure CLI defect,
just an empty bash variable with no early check.

Given this is the second time a documented variable turned out never to
have been set, the check was extended to every variable the workflow
actually consumes, not just the two the SQL bug happened to surface.
`ci-cd.yml` now fails loudly (`::error::` + `exit 1`) immediately if any of
`SQL_SERVER_FQDN`, `SQL_DATABASE_NAME`, `RESOURCE_GROUP`, or
`AZURE_SUBSCRIPTION_ID` is empty in the firewall-rule/migration steps, or if
`API_APP_SERVICE_NAME`/`WEB_APP_SERVICE_NAME` is empty in the respective
deploy job, before attempting anything against Azure. **Action needed:**
confirmed present on `development` so far are only `CI_AZURE_CLIENT_ID`,
`AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, and `RESOURCE_GROUP` — add or
confirm `SQL_SERVER_FQDN`, `SQL_DATABASE_NAME`, `API_APP_SERVICE_NAME`, and
`WEB_APP_SERVICE_NAME` (values in the table above), then trigger a fresh
`Run workflow` (not `Re-run failed jobs`) — see `docs/Backlog.md`.

1. **Create the CI/CD service principal + OIDC federation, and grant its two
   RBAC roles** (`Website Contributor` on the resource group, `SQL Server
   Contributor` on just the SQL server — the second is new, added post-wp-060
   so the pipeline can manage its own temporary firewall rule):
   ```bash
   az login --tenant 641fc267-7902-48d0-8e1c-1d3d0166c8ac --allow-no-subscriptions
   ./infra/scripts/setup-github-oidc-service-principal.sh \
     --tenant-id 641fc267-7902-48d0-8e1c-1d3d0166c8ac \
     --subscription-id ca6d83dc-24be-412f-a6f4-97da7a4abf5d \
     --resource-group rg-apflow-dev \
     --github-org <your-org-or-username> \
     --github-repo <your-repo-name> \
     --github-branch main \
     --sql-server-name sql-apflow-dev-ryd3y6fyfloxu
   ```
   Capture the printed `CI_AZURE_CLIENT_ID` for the Variables table above.
   If this has already been run once (creating the app registration) but
   without `--sql-server-name`, do not re-run the whole script — it would
   create a second, duplicate app registration. Instead grant just the
   missing role directly:
   ```bash
   az role assignment create \
     --assignee-object-id <existing CI SP object id> \
     --assignee-principal-type ServicePrincipal \
     --role "SQL Server Contributor" \
     --scope /subscriptions/ca6d83dc-24be-412f-a6f4-97da7a4abf5d/resourceGroups/rg-apflow-dev/providers/Microsoft.Sql/servers/sql-apflow-dev-ryd3y6fyfloxu
   ```

2. **Grant it SQL migration access** (same Query Editor / `sqlcmd` approach
   as WP-021's `grant-sql-managed-identity-access.sql`):
   ```bash
   sqlcmd -S sql-apflow-dev-ryd3y6fyfloxu.database.windows.net -d sqldb-apflow-dev \
     -G --authentication-method=ActiveDirectoryDefault \
     -i infra/scripts/grant-ci-sql-migration-access.sql
   ```

3. **Set all the Variables** listed above on a GitHub Environment named
   `development`.

4. **Push to `main`** (or run the workflow manually via **Actions → AP Flow
   CI/CD (Development) → Run workflow**).

---

## What "stop on failure" means here (Task 7)

No special logic was added — GitHub Actions' default job-dependency
behavior already does this: `migrate-development-database` only runs if
every `backend-build-test` matrix leg and `frontend-build-test` succeeded
(via `needs:`), and both `deploy-api`/`deploy-web` only run if the migration
step succeeded. A failing `dotnet test`, `eslint`, `tsc -b`, `vitest run`, or
`dotnet ef database update` all naturally stop the pipeline before anything
downstream runs, with no extra conditional logic needed.

---

## Flagged items — please verify on first real run

Three things in this pipeline are built to the best-known-correct pattern
but **could not be tested in this environment** (no live repo/Actions
access) — each is called out in the workflow file itself, repeated here for
visibility:

1. **Repo file paths** (`src/APFlow.Api`, `src/APFlow.Infrastructure`,
   `src/APFlow.Web`, the five `tests/*.Tests` paths). The `src/`/`tests/`
   paths mirror what `WP-012-Report.md` actually confirmed
   (`src/APFlow.Application`, `tests/APFlow.Application.Tests`, etc.).
   `APFlow.Web`'s frontend build output layout (`dist/`) was already
   correct; `server.js` did **not** exist until 2026-07-31 (see
   `docs/Backlog.md`'s Closed section) — it now does, ships an Express
   static server with SPA fallback, and `resources.bicep` sets
   `appCommandLine: 'node server.js'` so Azure actually runs it instead of
   falling back to its own generic static-site host.
   **`SCM_DO_BUILD_DURING_DEPLOYMENT` was tried as `true` first (so Oryx
   would install `node_modules` post-deploy) and broke the real deploy**:
   Oryx auto-runs `package.json`'s `"build"` script whenever one exists, so
   it re-ran `tsc -b && vite build` against an artifact that ships only the
   already-built `dist/`, not `tsconfig.json`/`src/` — `error TS5083: Cannot
   read file 'tsconfig.json'`, deploy failed. Fixed by building in CI (where
   the full source and devDependencies are present) and shipping a
   production-only `node_modules` (`npm ci --omit=dev`, after the build
   step) directly in the artifact instead — `SCM_DO_BUILD_DURING_DEPLOYMENT`
   is now `false`, so Azure does a plain zip deploy with no Oryx build step
   at all. This resolves the artifact-strategy question the DevOps engineer
   flagged as an open choice (Oryx-build vs. ship-`node_modules`) — the
   Oryx-build side turned out not to work for this app's specific
   `package.json` shape.
2. **The OIDC → `dotnet ef` federated-token hand-off**
   (`migrate-development-database` job). Manually requesting GitHub's OIDC
   token and pointing `AZURE_FEDERATED_TOKEN_FILE` at it is the documented
   mechanism Azure.Identity's `DefaultAzureCredential` uses for workload
   identity — but this specific sequence hasn't been run end-to-end. If the
   migration step fails with an authentication error, this hand-off is the
   first place to look.
3. **`Website Contributor` RBAC role** on the CI/CD service principal. This
   is the minimal built-in role expected to cover `azure/webapps-deploy`; if
   deployment fails with a permissions error, widen to `Contributor` scoped
   to the same resource group (`rg-apflow-dev`) rather than going
   subscription-wide.

---

## What's intentionally NOT in this WP

- **Staging/production pipelines** — no environment matrix exists; adding
  one is future work, deliberately not pre-built here (see the workflow's
  own comment on why `development` is hardcoded, not parameterized).
- **APFlow.Workers deployment** — WP-021 didn't provision any hosting target
  for background/async processing (no App Service, Container App, or
  Function App for Workers exists yet), so this pipeline builds and tests
  the Workers project (implicitly, via the solution build) but has nothing
  to deploy it to. Raise as a separate WP if Workers needs a live
  environment.
- **Database backup/rollback automation** — migrations are applied directly;
  no automated rollback-on-failure exists beyond EF Core's own transactional
  migration behavior. Worth a follow-up WP once real data exists to protect.
