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
#       --github-branch main
# ==============================================================================
set -euo pipefail

TENANT_ID=""
SUBSCRIPTION_ID=""
RESOURCE_GROUP=""
GITHUB_ORG=""
GITHUB_REPO=""
GITHUB_BRANCH="main"
APP_DISPLAY_NAME="APFlow-CI-Dev"

usage() {
  echo "Usage: $0 --tenant-id <id> --subscription-id <id> --resource-group <rg> --github-org <org> --github-repo <repo> [--github-branch <branch>]"
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
# 2. Add the federated credential trusting GitHub's OIDC issuer for this
#    specific repo + branch (not "any branch", not "any repo" — scoped
#    deliberately narrow).
# ------------------------------------------------------------------------------
echo "--> Adding federated credential for $GITHUB_ORG/$GITHUB_REPO (branch: $GITHUB_BRANCH)"
az ad app federated-credential create \
  --id "$CI_OBJECT_ID" \
  --parameters "{
    \"name\": \"github-actions-${GITHUB_BRANCH}\",
    \"issuer\": \"https://token.actions.githubusercontent.com\",
    \"subject\": \"repo:${GITHUB_ORG}/${GITHUB_REPO}:ref:refs/heads/${GITHUB_BRANCH}\",
    \"audiences\": [\"api://AzureADTokenExchange\"]
  }"

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
