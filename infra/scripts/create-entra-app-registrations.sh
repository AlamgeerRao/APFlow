#!/usr/bin/env bash
# ==============================================================================
# AP Flow — WP-021 Task 1: Create Entra App Registrations (dev)
#
# Creates exactly two App Registrations, per the work package's confirmed
# scope:
#   1. APFlow-SPA-Dev   — public client (no secret), used by the React SPA.
#   2. APFlow-Api-Dev   — confidential client, doing double duty as:
#        a) the OAuth2 resource exposing the "access_as_user" scope the SPA
#           calls APFlow.Api with, and
#        b) the Microsoft Graph app registration carrying the
#           Mail.ReadWrite (Application) permission + admin consent, used by
#           APFlow.Workers for app-only mail polling (client-credentials flow).
#
# PREREQUISITE THIS SCRIPT DOES NOT DO: creating the dev Entra External ID
# (CIAM) tenant itself. That step requires a human with the Tenant Creator /
# Global Administrator role in Microsoft Entra ID and is done once via the
# Azure Portal ("Create a tenant" > "Microsoft Entra External ID for customers").
# See README.md "STOP / escalation items" — this is the explicitly-permitted
# escalation from the work package.
#
# IMPORTANT ARCHITECTURAL NOTE — read before running the Graph section:
# Entra External ID (CIAM) tenants provide customer/application identity —
# they do NOT provision Exchange Online mailboxes. The Mail.ReadWrite
# permission this script grants is real, but it has no mailbox to target if
# left in the CIAM tenant. See README for the escalation raised on this point.
# Use --mail-tenant-id to point the Graph section at whichever tenant actually
# hosts the dev test mailbox once that's decided.
#
# Usage:
#   az login --tenant <ciam-tenant-id> --allow-no-subscriptions
#   ./create-entra-app-registrations.sh \
#       --tenant-id <ciam-tenant-id> \
#       [--mail-tenant-id <tenant-that-owns-the-dev-mailbox>] \
#       [--web-app-url https://app-apflow-dev-web-xxxx.azurewebsites.net] \
#       [--key-vault-name kv-apflow-dev-xxxx]
#
# Requires: Azure CLI, logged in as a user who can create app registrations
# (Application Developer role minimum) and, for the admin-consent step,
# Privileged Role Administrator / Global Administrator in the tenant granting
# Mail.ReadWrite.
# ==============================================================================
set -euo pipefail

TENANT_ID=""
MAIL_TENANT_ID=""
WEB_APP_URL=""
KEY_VAULT_NAME=""
SECRET_NAME=""
GRAPH_RESOURCE_APP_ID="00000003-0000-0000-c000-000000000000"

usage() {
  echo "Usage: $0 --tenant-id <id> [--mail-tenant-id <id>] [--web-app-url <url>] [--key-vault-name <name> --secret-name <name>]"
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tenant-id) TENANT_ID="$2"; shift 2 ;;
    --mail-tenant-id) MAIL_TENANT_ID="$2"; shift 2 ;;
    --web-app-url) WEB_APP_URL="$2"; shift 2 ;;
    --key-vault-name) KEY_VAULT_NAME="$2"; shift 2 ;;
    --secret-name) SECRET_NAME="$2"; shift 2 ;;
    *) usage ;;
  esac
done

[[ -z "$TENANT_ID" ]] && usage
[[ -z "$MAIL_TENANT_ID" ]] && MAIL_TENANT_ID="$TENANT_ID"

# The Key Vault secret's NAME is a separate, already-scoped task (naming
# convention e.g. graph-cred-{tenantId}) — this script provisions the value,
# not the naming decision. If storing in Key Vault, the caller must supply
# whatever name that other task has settled on.
if [[ -n "$KEY_VAULT_NAME" && -z "$SECRET_NAME" ]]; then
  echo "!! --key-vault-name was given without --secret-name."
  echo "   This script doesn't invent a secret-naming convention (that's a separate, already-scoped task)."
  echo "   Re-run with --secret-name <name-from-that-task>, or omit --key-vault-name to just print the secret."
  exit 1
fi

command -v az >/dev/null || { echo "Azure CLI not found."; exit 1; }
command -v uuidgen >/dev/null || { echo "uuidgen not found (needed to mint the OAuth scope id)."; exit 1; }

echo "== AP Flow Entra App Registration setup (dev) =="
echo "SPA/API tenant : $TENANT_ID"
echo "Graph/mail tenant : $MAIL_TENANT_ID"

# ------------------------------------------------------------------------------
# 1. SPA app registration (public client)
# ------------------------------------------------------------------------------
echo "--> Creating APFlow-SPA-Dev in tenant $TENANT_ID"
SPA_APP_ID=$(az ad app create --display-name "APFlow-SPA-Dev" --sign-in-audience AzureADMyOrg --query appId -o tsv)
SPA_OBJECT_ID=$(az ad app show --id "$SPA_APP_ID" --query id -o tsv)
az ad sp create --id "$SPA_APP_ID" >/dev/null 2>&1 || true # service principal may already exist

REDIRECT_URIS="\"http://localhost:5173\""
if [[ -n "$WEB_APP_URL" ]]; then
  REDIRECT_URIS="${REDIRECT_URIS},\"${WEB_APP_URL}\""
fi

az rest --method PATCH \
  --uri "https://graph.microsoft.com/v1.0/applications/${SPA_OBJECT_ID}" \
  --headers "Content-Type=application/json" \
  --body "{\"spa\":{\"redirectUris\":[${REDIRECT_URIS}]}}"

echo "    SPA client ID: $SPA_APP_ID"

# ------------------------------------------------------------------------------
# 2. API app registration (confidential client — resource + Graph app)
# ------------------------------------------------------------------------------
echo "--> Creating APFlow-Api-Dev in tenant $TENANT_ID"
API_APP_ID=$(az ad app create --display-name "APFlow-Api-Dev" --sign-in-audience AzureADMyOrg --query appId -o tsv)
API_OBJECT_ID=$(az ad app show --id "$API_APP_ID" --query id -o tsv)
az ad sp create --id "$API_APP_ID" >/dev/null 2>&1 || true

az ad app update --id "$API_APP_ID" --identifier-uris "api://${API_APP_ID}"

SCOPE_ID=$(uuidgen)
az rest --method PATCH \
  --uri "https://graph.microsoft.com/v1.0/applications/${API_OBJECT_ID}" \
  --headers "Content-Type=application/json" \
  --body "{\"api\":{\"oauth2PermissionScopes\":[{\"id\":\"${SCOPE_ID}\",\"adminConsentDescription\":\"Allow the app to access APFlow.Api on behalf of the signed-in user.\",\"adminConsentDisplayName\":\"Access APFlow.Api as user\",\"userConsentDescription\":\"Allow the app to access APFlow.Api on your behalf.\",\"userConsentDisplayName\":\"Access APFlow.Api\",\"isEnabled\":true,\"type\":\"User\",\"value\":\"access_as_user\"}]}}"

API_SCOPE="api://${API_APP_ID}/access_as_user"
echo "    API client ID: $API_APP_ID"
echo "    API scope    : $API_SCOPE"

# Let the SPA call the API scope, then grant admin consent for it.
az ad app permission add --id "$SPA_APP_ID" --api "$API_APP_ID" --api-permissions "${SCOPE_ID}=Scope"
az ad app permission admin-consent --id "$SPA_APP_ID" || {
  echo "    !! admin-consent for the SPA->API permission failed — needs a Privileged Role Administrator / Global Administrator to run:"
  echo "       az ad app permission admin-consent --id $SPA_APP_ID"
}

# ------------------------------------------------------------------------------
# 3. Microsoft Graph — Mail.ReadWrite (Application), NOT Mail.Read, on the API app
#    Runs against MAIL_TENANT_ID, which may differ from the SPA/API tenant.
# ------------------------------------------------------------------------------
if [[ "$MAIL_TENANT_ID" == "$TENANT_ID" ]]; then
  cat <<'WARN'

  ############################################################################
  # WARNING: --mail-tenant-id was not set, so the Graph permission below is
  # being configured in the SAME Entra External ID (CIAM) tenant as the SPA/API
  # registrations. CIAM tenants do not provision Exchange Online mailboxes —
  # there will be no mailbox for this permission to actually read/write.
  # This has been raised as a STOP/escalation item for the Chief Technical
  # Architect. Proceeding anyway per the work package's instruction not to
  # block on identity-provisioning ambiguity — do not treat this as "working"
  # until a real mail-hosting tenant is confirmed and re-run with
  # --mail-tenant-id pointed at it.
  ############################################################################

WARN
fi

echo "--> Granting Microsoft Graph Mail.ReadWrite (Application) to APFlow-Api-Dev in tenant $MAIL_TENANT_ID"
az login --tenant "$MAIL_TENANT_ID" --allow-no-subscriptions >/dev/null

GRAPH_SP_OBJECT_ID=$(az ad sp show --id "$GRAPH_RESOURCE_APP_ID" --query id -o tsv)
MAIL_READWRITE_ROLE_ID=$(az ad sp show --id "$GRAPH_RESOURCE_APP_ID" --query "appRoles[?value=='Mail.ReadWrite'].id | [0]" -o tsv)

if [[ -z "$MAIL_READWRITE_ROLE_ID" || "$MAIL_READWRITE_ROLE_ID" == "None" ]]; then
  echo "    !! Could not resolve the Mail.ReadWrite app role ID dynamically — do not hardcode a guessed GUID."
  echo "       Resolve manually via: az ad sp show --id $GRAPH_RESOURCE_APP_ID --query \"appRoles[?value=='Mail.ReadWrite']\""
  exit 1
fi

az ad app permission add --id "$API_APP_ID" --api "$GRAPH_RESOURCE_APP_ID" --api-permissions "${MAIL_READWRITE_ROLE_ID}=Role"
az ad app permission admin-consent --id "$API_APP_ID" || {
  echo "    !! Admin consent for Mail.ReadWrite failed — needs a Privileged Role Administrator / Global Administrator in tenant $MAIL_TENANT_ID to run:"
  echo "       az ad app permission admin-consent --id $API_APP_ID"
}

# ------------------------------------------------------------------------------
# 4. Client secret for the API app (confidential client — used for the Graph
#    client-credentials flow). This IS a secret: capture it now or via Key Vault.
# ------------------------------------------------------------------------------
echo "--> Generating a client secret for APFlow-Api-Dev"
GRAPH_CLIENT_SECRET=$(az ad app credential reset --id "$API_APP_ID" --display-name "graph-mail-readwrite" --years 1 --query password -o tsv)

if [[ -n "$KEY_VAULT_NAME" ]]; then
  echo "--> Storing the client secret in Key Vault '$KEY_VAULT_NAME' as '$SECRET_NAME'"
  az keyvault secret set --vault-name "$KEY_VAULT_NAME" --name "$SECRET_NAME" --value "$GRAPH_CLIENT_SECRET" >/dev/null
  echo "    Stored. The secret is not printed below."
else
  echo "    !! No --key-vault-name supplied. CAPTURE THIS SECRET NOW — it will not be shown again:"
  echo "    $GRAPH_CLIENT_SECRET"
fi

# ------------------------------------------------------------------------------
# Summary — non-secret reference values for the Backend and React Engineers
# ------------------------------------------------------------------------------
cat <<SUMMARY

================================================================================
Entra reference values (non-secret — safe to share with Backend/React Engineers)
================================================================================
Entra tenant ID (sign-in) : $TENANT_ID
Graph/mail tenant ID      : $MAIL_TENANT_ID
SPA client ID             : $SPA_APP_ID
SPA redirect URIs         : http://localhost:5173${WEB_APP_URL:+, $WEB_APP_URL}
API client ID             : $API_APP_ID
API scope (SPA -> API)    : $API_SCOPE
Graph permission granted  : Mail.ReadWrite (Application) — admin consent attempted, verify in portal
================================================================================
Reminder: if --web-app-url wasn't known yet when this ran, re-run the SPA
redirect PATCH once the App Service is deployed:
  az rest --method PATCH --uri https://graph.microsoft.com/v1.0/applications/$SPA_OBJECT_ID \
    --headers "Content-Type=application/json" \
    --body '{"spa":{"redirectUris":["http://localhost:5173","<final-web-app-url>"]}}'
================================================================================
SUMMARY
