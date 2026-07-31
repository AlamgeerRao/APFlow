#!/usr/bin/env bash
# ==============================================================================
# AP Flow — WP-022: Create the CI/CD Entra app registration for GitHub Actions
#
# This is a FOURTH app registration, distinct from WP-021's three (SPA, API,
# Graph). It represents the GitHub Actions pipeline itself — used only to log
# into Azure and deploy/migrate, never for user sign-in or mail access. It
# lives in the CIAM tenant because that's the tenant the Azure subscription
# itself belongs to (confirmed during WP-021 — "Alam Azure sub01" and the
# CIAM tenant are the same tenant), not because it's a customer-identity
# concern.
#
# NO CLIENT SECRET IS EVER CREATED for this app. Instead, a federated
# credential trusts GitHub's own OIDC token for this specific repo/branch —
# GitHub Actions proves its identity to Azure AD without any stored secret,
# matching this project's passwordless-by-design pattern everywhere else
# (AAD-only SQL, keyless Storage, RBAC-only Key Vault).
#
# PREREQUISITE THIS SCRIPT DOES NOT DO: nothing manual is required beyond
# running it — unlike WP-021's two tenant-creation STOP items, this is a
# same-tenant, same-subscription action the person running WP-021 already
# has the rights for (they created the CIAM tenant, so they're Global
# Administrator there). Still requires being run by a human, not this script
# alone — no code execution happens outside what you explicitly run.
#
# Usage:
#   az login --tenant <ciam-tenant-id> --allow-no-subscriptions
#   ./setup-github-oidc-service-principal.sh \
#       --tenant-id <ciam-tenant-id> \
#       --subscription-id <subscription-id> \
#       --resource-group rg-apflow-dev \
#       --github-org <your-github-org-or-username> \
#       --github-repo <your-repo-name> \
#       --github-branch main \
#       --sql-server-name sql-apflow-dev-ryd3y6fyfloxu
#
# --sql-server-name is optional but recommended: without it, the SQL Server
# Contributor grant (needed by ci-cd.yml's temporary firewall-rule steps,
# added post-wp-060) is skipped and printed as a follow-up command instead.
# ==============================================================================
set -euo pipefail

TENANT_ID=""
SUBSCRIPTION_ID=""
RESOURCE_GROUP=""
GITHUB_ORG=""
GITHUB_REPO=""
GITHUB_BRANCH="main"
GITHUB_ENVIRONMENT="development"
SQL_SERVER_NAME=""
APP_DISPLAY_NAME="APFlow-CI-Dev"

usage() {
  echo "Usage: $0 --tenant-id <id> --subscription-id <id> --resource-group <rg> --github-org <org> --github-repo <repo> [--github-branch <branch>] [--github-environment <name>] [--sql-server-name <name>]"
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tenant-id) TENANT_ID="$2"; shift 2 ;;
    --subscription-id) SUBSCRIPTION_ID="$2"; shift 2 ;;
    --resource-group) RESOURCE_GROUP="$2"; shift 2 ;;
    --github-org) GITHUB_ORG="$2"; shift 2 ;;
    --github-repo) GITHUB_REPO="$2"; shift 2 ;;
    --github-branch) GITHUB_BRANCH="$2"; shift 2 ;;
    --github-environment) GITHUB_ENVIRONMENT="$2"; shift 2 ;;
    --sql-server-name) SQL_SERVER_NAME="$2"; shift 2 ;;
    *) usage ;;
  esac
done

[[ -z "$TENANT_ID" || -z "$SUBSCRIPTION_ID" || -z "$RESOURCE_GROUP" || -z "$GITHUB_ORG" || -z "$GITHUB_REPO" ]] && usage

command -v az >/dev/null || { echo "Azure CLI not found."; exit 1; }

echo "== AP Flow CI/CD service principal setup =="
echo "Tenant         : $TENANT_ID"
echo "Subscription   : $SUBSCRIPTION_ID"
echo "Resource group : $RESOURCE_GROUP"
echo "GitHub repo     : $GITHUB_ORG/$GITHUB_REPO (branch: $GITHUB_BRANCH)"

# ------------------------------------------------------------------------------
# 1. Create the app registration (no redirect URIs, no secret — this is an
#    application-only identity used solely via federated credential + OIDC)
# ------------------------------------------------------------------------------
echo "--> Creating $APP_DISPLAY_NAME"
CI_APP_ID=$(az ad app create --display-name "$APP_DISPLAY_NAME" --sign-in-audience AzureADMyOrg --query appId -o tsv)
CI_OBJECT_ID=$(az ad app show --id "$CI_APP_ID" --query id -o tsv)
az ad sp create --id "$CI_APP_ID" >/dev/null 2>&1 || true
CI_SP_OBJECT_ID=$(az ad sp show --id "$CI_APP_ID" --query id -o tsv)

echo "    CI/CD app Client ID: $CI_APP_ID"

# ------------------------------------------------------------------------------
# 2. Add federated credentials trusting GitHub's OIDC issuer for this specific
#    repo (not "any repo" — scoped deliberately narrow). TWO are needed, not
#    one, because GitHub's OIDC token's "sub" claim depends on how the JOB is
#    configured, not just which branch triggered it:
#      - a job with no "environment:" key gets a REF-based subject
#        (repo:{org}/{repo}:ref:refs/heads/{branch})
#      - a job WITH "environment: <name>" gets an ENVIRONMENT-based subject
#        instead (repo:{org}/{repo}:environment:{name}) - this TAKES
#        PRIORITY over the ref-based shape, it does not additionally include it.
#    ci-cd.yml's migrate-development-database/deploy-api/deploy-web jobs all
#    specify "environment: name: development", so they need the second shape.
#    CONFIRMED LIVE (2026-07-31): this GitHub account also includes the
#    numeric owner/repo IDs in the subject (repo:{org}@{ownerId}/{repo}@{repoId}:...)
#    - seen directly in a real AADSTS700213 failure. Not documented as
#      universal GitHub behaviour, so this script creates BOTH the
#      id-qualified and plain forms for the environment-based credential, to
#      be safe regardless of which one a given GitHub account actually sends.
#    Owner/repo numeric IDs are fetched from GitHub's public API - no auth
#    needed for a public repo.
# ------------------------------------------------------------------------------
echo "--> Adding ref-based federated credential for $GITHUB_ORG/$GITHUB_REPO (branch: $GITHUB_BRANCH)"
az ad app federated-credential create \
  --id "$CI_OBJECT_ID" \
  --parameters "{
    \"name\": \"github-actions-${GITHUB_BRANCH}\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:${GITHUB_ORG}/${GITHUB_REPO}:ref:refs/heads/${GITHUB_BRANCH}\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"

echo "--> Adding environment-based federated credential for $GITHUB_ORG/$GITHUB_REPO (environment: $GITHUB_ENVIRONMENT)"
az ad app federated-credential create \
  --id "$CI_OBJECT_ID" \
  --parameters "{
    \"name\": \"github-actions-env-${GITHUB_ENVIRONMENT}\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:${GITHUB_ORG}/${GITHUB_REPO}:environment:${GITHUB_ENVIRONMENT}\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"

OWNER_ID=$(curl -sS "https://api.github.com/users/${GITHUB_ORG}" | grep -m1 '"id"' | grep -o '[0-9]*')
REPO_ID=$(curl -sS "https://api.github.com/repos/${GITHUB_ORG}/${GITHUB_REPO}" | grep -m1 '"id"' | grep -o '[0-9]*')
if [[ -n "$OWNER_ID" && -n "$REPO_ID" ]]; then
  echo "--> Adding id-qualified environment-based federated credential (owner id $OWNER_ID, repo id $REPO_ID)"
  az ad app federated-credential create \
    --id "$CI_OBJECT_ID" \
    --parameters "{
      \"name\": \"github-actions-env-${GITHUB_ENVIRONMENT}-idscoped\",
      \"issuer\": \"https://token.actions.githubusercontent.com\",
      \"subject\": \"repo:${GITHUB_ORG}@${OWNER_ID}/${GITHUB_REPO}@${REPO_ID}:environment:${GITHUB_ENVIRONMENT}\",
      \"audiences\": [\"api://AzureADTokenExchange\"]
    }"
else
  echo "--> Skipping id-qualified federated credential: could not resolve owner/repo numeric IDs from GitHub's public API."
fi

# ------------------------------------------------------------------------------
# 3. Grant RBAC — scoped to the resource group only, "Website Contributor"
#    (enough to deploy to App Service without subscription-wide Contributor).
#    FLAGGED: this is the minimal role I expect to be sufficient for
#    azure/webapps-deploy; not independently verified end-to-end — if the
#    first real deployment run reports a permissions error, widen to
#    "Contributor" scoped to the same resource group rather than going
#    subscription-wide.
# ------------------------------------------------------------------------------
echo "--> Granting 'Website Contributor' on resource group $RESOURCE_GROUP"
az role assignment create \
  --assignee-object-id "$CI_SP_OBJECT_ID" \
  --assignee-principal-type ServicePrincipal \
  --role "Website Contributor" \
  --scope "/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}"

# ------------------------------------------------------------------------------
# 4. Grant 'SQL Server Contributor', scoped to just the SQL server resource
#    (not the whole resource group - narrower than step 3's grant, same
#    least-privilege intent). Needed so the pipeline can add/remove a
#    temporary firewall rule for its own ephemeral runner IP around each
#    "dotnet ef database update" (see ci-cd.yml's "Add/Remove temporary SQL
#    firewall rule" steps) - without this, every migration run fails with
#    SqlException error 40 ("Could not open a connection to SQL Server"),
#    because GitHub-hosted runners have no static IP and are not covered by
#    the server's "Allow Azure services" firewall rule. This does NOT grant
#    any access to the data itself - that's governed separately by the SQL
#    contained database user from grant-ci-sql-migration-access.sql.
# ------------------------------------------------------------------------------
if [[ -n "$SQL_SERVER_NAME" ]]; then
  echo "--> Granting 'SQL Server Contributor' on SQL server $SQL_SERVER_NAME"
  az role assignment create \
    --assignee-object-id "$CI_SP_OBJECT_ID" \
    --assignee-principal-type ServicePrincipal \
    --role "SQL Server Contributor" \
    --scope "/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}/providers/Microsoft.Sql/servers/${SQL_SERVER_NAME}"
else
  echo "--> Skipping 'SQL Server Contributor' grant: no --sql-server-name given."
  echo "    Run this once manually before the pipeline can manage its own firewall rule:"
  echo "    az role assignment create --assignee-object-id $CI_SP_OBJECT_ID --assignee-principal-type ServicePrincipal --role \"SQL Server Contributor\" --scope /subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}/providers/Microsoft.Sql/servers/<sql-server-name>"
fi

# ------------------------------------------------------------------------------
# Summary
# ------------------------------------------------------------------------------
cat <<SUMMARY

================================================================================
CI/CD service principal — non-secret reference values for GitHub Variables
================================================================================
CI_AZURE_CLIENT_ID     : $CI_APP_ID
AZURE_TENANT_ID        : $TENANT_ID
AZURE_SUBSCRIPTION_ID  : $SUBSCRIPTION_ID
================================================================================
No client secret was created — none is needed. GitHub authenticates to Azure
via OIDC using the federated credential above.

Next step: grant this same identity a SQL contained database user with
migration rights — see infra/scripts/grant-ci-sql-migration-access.sql.
================================================================================
SUMMARY
