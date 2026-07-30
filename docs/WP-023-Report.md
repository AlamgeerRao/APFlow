# WP-023 — Application Configuration & Secrets: Report

**Status:** Partially complete — see "Flagged, not resolved" below before
treating this as fully closed.

---

## Confirmed rather than rebuilt (per the WP's own instruction)

Checked against WP-021's actual deployed state, not assumed:

- ✅ Key Vault (`kv-apflow-dev-ryd3y6`) deployed, RBAC authorization model.
- ✅ Graph secret correctly named `graph-secret-1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf`
  (confirmed live in Key Vault as of WP-021d's correction).
- ✅ SQL is Azure-AD-only / managed-identity — no SQL secret exists or is
  needed.
- ✅ Storage uses RBAC (`allowSharedKeyAccess: false`) — no Storage secret
  exists or is needed.

None of the above was touched or re-provisioned.

---

## Files created

| File | Task |
|---|---|
| `docs/Secret-Naming-Convention.md` | Tasks 3 & 4 — the master naming reference, covering current secrets, the platform-wide vs. per-tenant ruling, the `docintel` abbreviation exception, how to extend the pattern to a new connector, and how it extends across Development/Test/Production |
| `infra/scripts/set-platform-ai-secrets.sh` | Task 1 — stores `docintel-secret`/`openai-secret` and wires their non-secret endpoints into `APFlow.Api`'s app settings |
| `infra/scripts/verify-cors-live.sh` | Task 2 — live CORS verification (see below) |
| This report | Summary, validation steps, flagged items |

No files were modified — WP-021's `infra/` tree is unchanged; this WP only
adds new, separate files.

---

## Task 1 — Key Vault entries for Document Intelligence and OpenAI

**Design decision on "API key/endpoint for each":** the WP specifies one
secret name per service (`docintel-secret`, `openai-secret` — singular).
Endpoints are not secrets (plain URLs), so following the same treatment as
`SQL_SERVER_FQDN`/`STORAGE_BLOB_ENDPOINT` elsewhere in this project: **only
the API keys go into Key Vault** under the two named secrets; the two
**endpoints go into `APFlow.Api`'s app settings as plain, non-secret values**
(`DOCINTEL_ENDPOINT`, `OPENAI_ENDPOINT`). `infra/scripts/set-platform-ai-secrets.sh`
does both in one run.

**RBAC:** none needed. `APFlow.Api`'s managed identity already holds Key
Vault Secrets User on the whole vault from WP-021 — that grant is vault-wide,
not per-secret, so it automatically covers these two new secrets.

---

## Task 2 — Confirming `Cors:AllowedOrigins` is live

**What this actually needed, and the gap in verifying it:** the WP asks to
confirm the *deployed* App Service reflects this setting, not just the repo.
`infra/scripts/verify-cors-live.sh` performs three checks: (1) whether
`Cors__AllowedOrigins` exists in the live app settings, (2) Azure App
Service's *separate*, code-independent platform CORS feature (checked for
completeness — this is a different mechanism from an app-level CORS policy
and shouldn't be mistaken for it), and (3) an actual live OPTIONS/preflight
request against the deployed API, checking the real
`Access-Control-Allow-Origin` response header.

**Not resolved by this WP package alone — needs to actually be run.** No
live Azure access exists in this authoring environment (consistent with
every prior WP — deployment steps are always handed to a human to execute).
**Explicit confirmation of the CORS value being live requires running
`verify-cors-live.sh` (or the equivalent manual checks) against the real
deployed environment** — this has not been done as part of producing this
package. Run it, then this section can be updated with a genuine pass/fail.

**Additional gap:** I could not confirm which CORS mechanism `APFlow.Api`'s
`Program.cs` actually implements — the Filesystem connector to
`C:\Development\APFlow` became unresponsive partway through this WP after
successfully reading `docs\AI`, before I could inspect the actual code or
find the WP-059 report referenced in the task text. The verification script
checks for the config-driven pattern the WP's own wording implies
(`Cors:AllowedOrigins` → `Cors__AllowedOrigins`), but this should be
cross-checked against the real `Program.cs` before relying on it.

---

## Task 3 — Development/Test/Production secret-naming design

Documented in `docs/Secret-Naming-Convention.md` §5. Summary: **no change to
any secret's name across environments** — environment isolation happens
entirely at the Key Vault level (one vault per environment, same naming
pattern reused identically in each), never by embedding an environment label
into the secret name itself. This is a **design only** — no Test or
Production Key Vault has been provisioned; that's future work using
WP-021's `main.bicep` once its environment lock is extended.

---

## Task 4 — Single configuration guide

`docs/Secret-Naming-Convention.md` is the complete, single reference — every
current secret, the abbreviation exception, the extension procedure for a
new connector (walked through using the future Sage secret as the worked
example), and the cross-environment design from Task 3. This supersedes any
naming guidance previously scattered across `infra/README.md`,
`infra/docs/M365-Dev-Mailbox-Tenant.md`, and individual scripts' comments —
those files' own naming mentions remain accurate but this document is now
the authoritative source if anything ever conflicts.

---

## Validation steps

```bash
# Task 1 — secrets exist and are named correctly
az keyvault secret list --vault-name kv-apflow-dev-ryd3y6 --query "[].name" -o table
# Expect: graph-secret-1df7da13-..., docintel-secret, openai-secret

# Task 1 — retrievable by APFlow.Api's managed identity at runtime (no new
# RBAC needed, but worth confirming explicitly rather than assuming)
az role assignment list \
  --scope /subscriptions/ca6d83dc-24be-412f-a6f4-97da7a4abf5d/resourceGroups/rg-apflow-dev/providers/Microsoft.KeyVault/vaults/kv-apflow-dev-ryd3y6 \
  --query "[].{PrincipalId:principalId, Role:roleDefinitionName}" -o table
# Expect: the API app's principal ID (ba911baf-fdaf-42fa-917e-8e68429d818d,
# confirmed in WP-021) holding Key Vault Secrets User — already true before
# this WP, unaffected by adding the two new secrets to the same vault.

# Task 1 — endpoints landed as plain app settings, not secrets
az webapp config appsettings list -g rg-apflow-dev -n app-apflow-dev-api-ryd3y6fyfloxu \
  --query "[?name=='DOCINTEL_ENDPOINT' || name=='OPENAI_ENDPOINT']" -o table

# Task 2 — run the live CORS check
./infra/scripts/verify-cors-live.sh \
  --resource-group rg-apflow-dev \
  --api-app-service-name app-apflow-dev-api-ryd3y6fyfloxu \
  --expected-origin https://app-apflow-dev-web-ryd3y6fyfloxu.azurewebsites.net
```

---

## Flagged, not resolved

1. **Azure AI Document Intelligence and Azure OpenAI resources may not exist
   yet.** WP-021's resource list didn't include them, and WP-023's task list
   asks to "add Key Vault entries for" these services, not to provision the
   underlying Cognitive Services resources themselves. `set-platform-ai-secrets.sh`
   assumes real key/endpoint values are supplied — if the resources don't
   exist, this needs to go to the Chief Technical Architect as a scope
   question (a small additional WP to provision them, most likely) before
   Task 1 can actually be executed, not guessed at here.
2. **Task 2's live confirmation has not actually been run** — the script
   exists; running it against the real environment and recording a genuine
   pass/fail is the remaining step.
3. **`Program.cs`'s actual CORS implementation was not confirmed** against
   the real repo — the Filesystem connector to `C:\Development\APFlow`
   stopped responding after reading `docs\AI` successfully. Worth a quick
   look before fully trusting Check 1 of `verify-cors-live.sh`.
4. **The WP-059 report referenced in Task 2's wording was not located** —
   for the same reason as #3. If it contains a specific ask beyond "confirm
   via the Configuration blade or a live request test" (already covered
   above), that detail may be missing here.
