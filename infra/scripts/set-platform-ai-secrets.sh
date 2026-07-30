#!/usr/bin/env bash
# ==============================================================================
# AP Flow — WP-023 Task 1: Store platform-wide AI service secrets in Key Vault
#
# Stores docintel-secret and openai-secret (API keys only — see naming
# convention: docs/Secret-Naming-Convention.md §2) in the Development Key
# Vault, and wires the two service ENDPOINTS (non-secret — plain URLs, same
# treatment as SQL_SERVER_FQDN/STORAGE_BLOB_ENDPOINT in WP-021) into
# APFlow.Api's app settings.
#
# PREREQUISITE THIS SCRIPT ASSUMES BUT DOES NOT PROVIDE: the Azure AI
# Document Intelligence and Azure OpenAI resources themselves. Neither was
# in WP-021's resource list (Resource Group, App Service Plan, 2 App
# Services, SQL, Storage, Key Vault, App Insights, Log Analytics only), and
# WP-023's own task list asks to "add Key Vault entries for" these services,
# not to provision the underlying Cognitive Services resources. If they
# don't exist yet, this script cannot run — provisioning them is a gap to
# raise with the Chief Technical Architect, not something to guess at here.
# See the WP-023 report's "Flagged — not resolved" section.
#
# No RBAC changes are needed for this step: APFlow.Api's managed identity
# already holds Key Vault Secrets User on the whole vault (WP-021), which
# covers any secret added to it, including these two.
#
# Usage:
#   ./set-platform-ai-secrets.sh \
#     --key-vault-name kv-apflow-dev-ryd3y6 \
#     --api-app-service-name app-apflow-dev-api-ryd3y6fyfloxu \
#     --resource-group rg-apflow-dev \
#     --docintel-key '<key>' --docintel-endpoint 'https://<resource>.cognitiveservices.azure.com/' \
#     --openai-key '<key>' --openai-endpoint 'https://<resource>.openai.azure.com/'
#
# Never pass key values on a shared/logged terminal, CI log, or chat channel
# — run this yourself, directly, the same way WP-021's Graph secret was
# stored.
# ==============================================================================
set -euo pipefail

KEY_VAULT_NAME=""
API_APP_SERVICE_NAME=""
RESOURCE_GROUP=""
DOCINTEL_KEY=""
DOCINTEL_ENDPOINT=""
OPENAI_KEY=""
OPENAI_ENDPOINT=""

usage() {
  echo "Usage: $0 --key-vault-name <name> --api-app-service-name <name> --resource-group <rg> --docintel-key <key> --docintel-endpoint <url> --openai-key <key> --openai-endpoint <url>"
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --key-vault-name) KEY_VAULT_NAME="$2"; shift 2 ;;
    --api-app-service-name) API_APP_SERVICE_NAME="$2"; shift 2 ;;
    --resource-group) RESOURCE_GROUP="$2"; shift 2 ;;
    --docintel-key) DOCINTEL_KEY="$2"; shift 2 ;;
    --docintel-endpoint) DOCINTEL_ENDPOINT="$2"; shift 2 ;;
    --openai-key) OPENAI_KEY="$2"; shift 2 ;;
    --openai-endpoint) OPENAI_ENDPOINT="$2"; shift 2 ;;
    *) usage ;;
  esac
done

[[ -z "$KEY_VAULT_NAME" || -z "$API_APP_SERVICE_NAME" || -z "$RESOURCE_GROUP" || -z "$DOCINTEL_KEY" || -z "$DOCINTEL_ENDPOINT" || -z "$OPENAI_KEY" || -z "$OPENAI_ENDPOINT" ]] && usage

echo "== AP Flow platform AI secrets setup =="
echo "Key Vault      : $KEY_VAULT_NAME"
echo "API App Service: $API_APP_SERVICE_NAME"

# ------------------------------------------------------------------------------
# 1. API keys -> Key Vault (per docs/Secret-Naming-Convention.md — no tenant
#    suffix, these are platform-wide)
# ------------------------------------------------------------------------------
echo "--> Storing docintel-secret"
az keyvault secret set --vault-name "$KEY_VAULT_NAME" --name "docintel-secret" --value "$DOCINTEL_KEY" >/dev/null

echo "--> Storing openai-secret"
az keyvault secret set --vault-name "$KEY_VAULT_NAME" --name "openai-secret" --value "$OPENAI_KEY" >/dev/null

# ------------------------------------------------------------------------------
# 2. Endpoints -> APFlow.Api app settings (non-secret — same treatment as
#    SQL_SERVER_FQDN / STORAGE_BLOB_ENDPOINT elsewhere in this project)
# ------------------------------------------------------------------------------
echo "--> Setting DOCINTEL_ENDPOINT and OPENAI_ENDPOINT app settings on $API_APP_SERVICE_NAME"
az webapp config appsettings set \
  --resource-group "$RESOURCE_GROUP" \
  --name "$API_APP_SERVICE_NAME" \
  --settings \
    "DOCINTEL_ENDPOINT=$DOCINTEL_ENDPOINT" \
    "OPENAI_ENDPOINT=$OPENAI_ENDPOINT" \
  >/dev/null

echo ""
echo "Done. No new RBAC grant was needed — APFlow.Api's managed identity"
echo "already holds Key Vault Secrets User on the whole vault (WP-021)."
echo ""
echo "Verify with:"
echo "  az keyvault secret list --vault-name $KEY_VAULT_NAME --query \"[].name\" -o table"
echo "  az webapp config appsettings list -g $RESOURCE_GROUP -n $API_APP_SERVICE_NAME --query \"[?name=='DOCINTEL_ENDPOINT' || name=='OPENAI_ENDPOINT']\" -o table"
