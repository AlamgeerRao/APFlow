# AP Flow — WP-022: CI/CD Pipeline (Development)

Scope: **Development environment only**, deploying to the two App Services
provisioned in WP-021:

```
API:  https://app-apflow-dev-api-ryd3y6fyfloxu.azurewebsites.net
Web:  https://app-apflow-dev-web-ryd3y6fyfloxu.azurewebsites.net
```

---

## Files created

| File | Purpose |
|---|---|
| `.github/workflows/ci-cd.yml` | The pipeline itself |
| `infra/scripts/setup-github-oidc-service-principal.sh` | One-time setup: creates the CI/CD Entra app registration + OIDC federation + RBAC |
| `infra/scripts/grant-ci-sql-migration-access.sql` | One-time setup: grants that identity a database user with migration rights |
| `docs/CI-CD-Pipeline.md` | This document |

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
| `KEY_VAULT_NAME` | `kv-apflow-dev-ryd3y6` | WP-021 |
| `STORAGE_ACCOUNT_NAME` | `stapflowdevryd3y6fyfloxu` | WP-021 |
| `STORAGE_BLOB_ENDPOINT` | `https://stapflowdevryd3y6fyfloxu.blob.core.windows.net/` | WP-021 |
| `ENTRA_SPA_CLIENT_ID` | `d47fcb44-752e-4d7a-ac49-d3c71dfca7e0` | WP-021 |
| `ENTRA_API_CLIENT_ID` | `603682ec-46ab-4075-9e87-8e44478a39a4` | WP-021 |
| `ENTRA_API_SCOPE` | `api://603682ec-46ab-4075-9e87-8e44478a39a4/access_as_user` | WP-021 |
| `GRAPH_TENANT_ID` | `1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf` | WP-021 |
| `GRAPH_CLIENT_ID` | `40d63c64-ff18-4028-ba92-01ca93c1c432` | WP-021 |
| `GRAPH_MAILBOX_UPN` | `invoices@acoounts01.onmicrosoft.com` | WP-021 |
| `GRAPH_CLIENT_SECRET_KEYVAULT_NAME` | `graph-secret-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf` | WP-021d — a **name**, not the secret itself; the app reads the actual value from Key Vault at runtime. (Corrected from an earlier `wp-021c` naming mistake, caught by QA — `graph-cred-` never was a real convention; see WP-021's README "What changed in wp-021d".) |

The current workflow only actually *uses* `AZURE_TENANT_ID`,
`AZURE_SUBSCRIPTION_ID`, `CI_AZURE_CLIENT_ID`, `API_APP_SERVICE_NAME`,
`WEB_APP_SERVICE_NAME`, `SQL_SERVER_FQDN`, and `SQL_DATABASE_NAME` directly —
the rest are documented here for completeness/handoff (per Task 8's ask) and
because the Entra/Graph/Storage values are things the Backend and React
Engineers will need to configure their own code against, even though the
pipeline itself doesn't reference them.

### Repository/Environment Secrets (sensitive)

**None required.** This is intentional, not incomplete — see the correction
above. If a future change reintroduces a stored credential (e.g. a
third-party API key with no OIDC/managed-identity option), add it here as an
actual GitHub *Secret*, scoped to the `development` environment.

---

## One-time setup (run before the first pipeline run)

1. **Create the CI/CD service principal + OIDC federation:**
   ```bash
   az login --tenant 641fc267-7902-48d0-8e1c-1d3d0166c8ac --allow-no-subscriptions
   ./infra/scripts/setup-github-oidc-service-principal.sh \
     --tenant-id 641fc267-7902-48d0-8e1c-1d3d0166c8ac \
     --subscription-id ca6d83dc-24be-412f-a6f4-97da7a4abf5d \
     --resource-group rg-apflow-dev \
     --github-org <your-org-or-username> \
     --github-repo <your-repo-name> \
     --github-branch main
   ```
   Capture the printed `CI_AZURE_CLIENT_ID` for the Variables table above.

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
   (`src/APFlow.Application`, `tests/APFlow.Application.Tests`, etc.), but
   `APFlow.Web`'s exact path and the frontend build output layout
   (`dist/`, `server.js`) are **assumed**, not confirmed against the real
   repo. Adjust the `WEB_APP_PATH` env var and the `web-publish` artifact
   paths if they differ.
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
