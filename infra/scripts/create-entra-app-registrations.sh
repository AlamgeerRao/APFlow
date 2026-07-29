#!/usr/bin/env bash
# ==============================================================================
# AP Flow — WP-021 Task 1: Create Entra App Registrations (dev)
#
# Creates THREE App Registrations — revised per Chief Technical Architect
# ruling (overriding this WP's original "combine API + Graph" interim
# assumption): the API resource and the Graph application-only client are two
# different security boundaries doing two different jobs (validating
# end-user tokens vs. a no-user-context mailbox credential) and must not
# share a registration.
#
#   1. APFlow-SPA-Dev    — public client (no secret), used by the React SPA.
#                           Lives in the dev Entra External ID (CIAM) tenant.
#   2. APFlow-Api-Dev     — confidential client; exposes the "access_as_user"
#                           scope the SPA calls APFlow.Api with. Lives in the
#                           same CIAM tenant as the SPA. Carries NO Graph
#                           permissions and no Graph secret.
#   3. Graph app          — application-only client carrying Microsoft Graph's
#                           Mail.ReadWrite (Application) permission + admin
#                           consent, used by APFlow.Workers for app-only mail
#                           polling (client-credentials flow). Lives in a
#                           separate M365 tenant with a real mailbox — see
#                           ../docs/M365-Dev-Mailbox-Tenant.md for how that
#                           tenant was actually provisioned (the free
#                           Developer Program sandbox was tried first and
#                           found ineligible — a 2024 Microsoft policy change
#                           restricts it to qualifying Visual Studio/Partner
#                           accounts; Business Basic on monthly billing was
#                           used instead and is the recommended default).
#
# THIS DEV INSTANCE ALREADY HAS A GRAPH APP REGISTRATION, CREATED MANUALLY.
# See ../docs/M365-Dev-Mailbox-Tenant.md for the full record. Pass its
# Client ID via --graph-client-id and this script will skip re-creating it
# (see step 3 below) — it will not touch its permissions or reset its
# secret. Omit --graph-client-id only when standing up a genuinely new
# environment that doesn't have one yet.
#
# PREREQUISITES THIS SCRIPT DOES NOT DO (both require a human):
#   - Creating the dev Entra External ID (CIAM) tenant (STOP item 1) —
#     already done for this environment; see README "Confirmed reference
#     values".
#   - Provisioning an M365 tenant with a real mailbox (STOP item 2) —
#     already done for this environment via Business Basic; see
#     ../docs/M365-Dev-Mailbox-Tenant.md. If Graph app creation is left to
#     this script (--graph-client-id omitted) for a NEW environment, that
#     tenant must exist first regardless.
#
# Usage (this environment — reusing the existing Graph app registration):
#   az login --tenant <ciam-tenant-id> --allow-no-subscriptions
#   ./create-entra-app-registrations.sh \
#       --tenant-id <ciam-tenant-id> \
#       --mail-tenant-id <m365-tenant-id> \
#       --graph-client-id <existing-graph-app-client-id> \
#       [--web-app-url https://app-apflow-dev-web-xxxx.azurewebsites.net]
#
# Usage (a NEW environment — no Graph app exists yet, let the script create one):
#   ./create-entra-app-registrations.sh \
#       --tenant-id <ciam-tenant-id> \
#       --mail-tenant-id <new-m365-tenant-id> \
#       [--web-app-url https://app-apflow-dev-web-xxxx.azurewebsites.net] \
#       [--key-vault-name kv-apflow-dev-xxxx --secret-name <name-from-the-separate-naming-task>] \
#       [--subscription-tenant-id <tenant-that-owns-the-Azure-subscription/Key-Vault>]
#
# Requires: Azure CLI, logged in as a user who can create app registrations
# (Application Developer role minimum) in both the CIAM tenant and the
# mail-hosting tenant, and, for the admin-consent steps, Privileged Role
# Administrator / Global Administrator in each respective tenant.
# ==============================================================================
set -euo pipefail

TENANT_ID=""
MAIL_TENANT_ID=""
SUBSCRIPTION_TENANT_ID=""
WEB_APP_URL=""
KEY_VAULT_NAME=""
SECRET_NAME=""
EXISTING_GRAPH_CLIENT_ID=""
GRAPH_RESOURCE_APP_ID="00000003-0000-0000-c000-000000000000"

usage() {
  echo "Usage: $0 --tenant-id <id> --mail-tenant-id <id> [--graph-client-id <id>] [--web-app-url <url>] [--key-vault-name <name> --secret-name <name>] [--subscription-tenant-id <id>]"
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --tenant-id) TENANT_ID="$2"; shift 2 ;;
    --mail-tenant-id) MAIL_TENANT_ID="$2"; shift 2 ;;
    --subscription-tenant-id) SUBSCRIPTION_TENANT_ID="$2"; shift 2 ;;
    --graph-client-id) EXISTING_GRAPH_CLIENT_ID="$2"; shift 2 ;;
    --web-app-url) WEB_APP_URL="$2"; shift 2 ;;
    --key-vault-name) KEY_VAULT_NAME="$2"; shift 2 ;;
    --secret-name) SECRET_NAME="$2"; shift 2 ;;
    *) usage ;;
  esac
done

[[ -z "$TENANT_ID" ]] && usage

# Per Chief Technical Architect ruling on STOP item 2: the Graph/mail piece
# must target a real mailbox-hosting tenant, not the CIAM tenant and not
# deferred. Required argument — no defaults-to-same-tenant fallback.
if [[ -z "$MAIL_TENANT_ID" ]]; then
  echo "!! --mail-tenant-id is required."
  echo "   Per Chief Technical Architect ruling, this must be a real M365 tenant with an"
  echo "   actual Exchange Online mailbox — not the CIAM tenant from --tenant-id."
  echo "   See ../docs/M365-Dev-Mailbox-Tenant.md for how to provision one (the free"
  echo "   Developer Program sandbox is likely ineligible per a 2024 Microsoft policy"
  echo "   change — Business Basic on monthly billing is the recommended route)."
  exit 1
fi

if [[ "$MAIL_TENANT_ID" == "$TENANT_ID" ]]; then
  echo "!! --mail-tenant-id matches --tenant-id (the CIAM tenant)."
  echo "   CIAM tenants have no Exchange Online mailboxes — Mail.ReadWrite would have"
  echo "   nothing to target. Point --mail-tenant-id at the mailbox-hosting tenant"
  echo "   instead, per the Chief Technical Architect's ruling."
  exit 1
fi

# The Key Vault secret's NAME is a separate, already-scoped task (naming
# convention e.g. graph-cred-{tenantId}) — this script provisions the value,
# not the naming decision. Only relevant when this script is generating a NEW
# Graph app secret (i.e. --graph-client-id was NOT supplied).
if [[ -z "$EXISTING_GRAPH_CLIENT_ID" && -n "$KEY_VAULT_NAME" && -z "$SECRET_NAME" ]]; then
  echo "!! --key-vault-name was given without --secret-name."
  echo "   This script doesn't invent a secret-naming convention (that's a separate, already-scoped task)."
  echo "   Re-run with --secret-name <name-from-that-task>, or omit --key-vault-name to just print the secret."
  exit 1
fi

command -v az >/dev/null || { echo "Azure CLI not found."; exit 1; }
command -v uuidgen >/dev/null || { echo "uuidgen not found (needed to mint the OAuth scope id)."; exit 1; }

echo "== AP Flow Entra App Registration setup (dev) =="
echo "SPA/API tenant (CIAM)   : $TENANT_ID"
echo "Graph/mail tenant       : $MAIL_TENANT_ID"
[[ -n "$EXISTING_GRAPH_CLIENT_ID" ]] && echo "Graph app               : reusing existing $EXISTING_GRAPH_CLIENT_ID (not modifying permissions/secret)"

# ------------------------------------------------------------------------------
# 1. SPA app registration (public client) — CIAM tenant
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
# 2. API app registration (confidential client — resource/scope ONLY) — CIAM tenant
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
# 3. Graph app registration (application-only) — mail-hosting tenant
#    Separate security boundary from the API app: a leaked Graph secret must
#    not compromise API token validation, and vice versa.
#
#    If --graph-client-id was supplied, this whole step is SKIPPED: this dev
#    environment already has one (created manually — see
#    ../docs/M365-Dev-Mailbox-Tenant.md), with Mail.ReadWrite (Application)
#    already granted with admin consent and a client secret already issued.
#    This script does not touch its permissions or reset its secret.
# ------------------------------------------------------------------------------
if [[ -n "$EXISTING_GRAPH_CLIENT_ID" ]]; then
  GRAPH_APP_ID="$EXISTING_GRAPH_CLIENT_ID"
  echo "--> Reusing existing Graph app $GRAPH_APP_ID in tenant $MAIL_TENANT_ID (skipping creation/permission/secret steps)"
else
  echo "--> Switching context to Graph/mail tenant $MAIL_TENANT_ID"
  az login --tenant "$MAIL_TENANT_ID" --allow-no-subscriptions >/dev/null

  echo "--> Creating APFlow-Graph-Dev in tenant $MAIL_TENANT_ID"
  GRAPH_APP_ID=$(az ad app create --display-name "APFlow-Graph-Dev" --sign-in-audience AzureADMyOrg --query appId -o tsv)
  az ad sp create --id "$GRAPH_APP_ID" >/dev/null 2>&1 || true

  GRAPH_SP_OBJECT_ID=$(az ad sp show --id "$GRAPH_RESOURCE_APP_ID" --query id -o tsv)
  MAIL_READWRITE_ROLE_ID=$(az ad sp show --id "$GRAPH_RESOURCE_APP_ID" --query "appRoles[?value=='Mail.ReadWrite'].id | [0]" -o tsv)

  if [[ -z "$MAIL_READWRITE_ROLE_ID" || "$MAIL_READWRITE_ROLE_ID" == "None" ]]; then
    echo "    !! Could not resolve the Mail.ReadWrite app role ID dynamically — do not hardcode a guessed GUID."
    echo "       Resolve manually via: az ad sp show --id $GRAPH_RESOURCE_APP_ID --query \"appRoles[?value=='Mail.ReadWrite']\""
    exit 1
  fi

  echo "--> Granting Microsoft Graph Mail.ReadWrite (Application) — NOT Mail.Read — to APFlow-Graph-Dev"
  az ad app permission add --id "$GRAPH_APP_ID" --api "$GRAPH_RESOURCE_APP_ID" --api-permissions "${MAIL_READWRITE_ROLE_ID}=Role"
  az ad app permission admin-consent --id "$GRAPH_APP_ID" || {
    echo "    !! Admin consent for Mail.ReadWrite failed — needs a Privileged Role Administrator / Global Administrator in tenant $MAIL_TENANT_ID to run:"
    echo "       az ad app permission admin-consent --id $GRAPH_APP_ID"
  }

  echo "--> Generating a client secret for APFlow-Graph-Dev"
  GRAPH_CLIENT_SECRET=$(az ad app credential reset --id "$GRAPH_APP_ID" --display-name "graph-mail-readwrite" --years 1 --query password -o tsv)
fi

# ------------------------------------------------------------------------------
# 4. Store the Graph client secret in Key Vault.
#    - New Graph app (this run generated GRAPH_CLIENT_SECRET above): stored
#      automatically if --key-vault-name/--secret-name were given.
#    - Existing Graph app (--graph-client-id): this script never saw the
#      secret value and never will — it was captured once at creation time,
#      outside this script, per ../docs/M365-Dev-Mailbox-Tenant.md. Store it
#      yourself, directly from your own terminal — never paste a secret
#      value into an AI chat or any other transient channel:
#        az keyvault secret set --vault-name <kv> --name <secret-name> --value <the-value-you-already-have>
# ------------------------------------------------------------------------------
if [[ -z "$EXISTING_GRAPH_CLIENT_ID" ]]; then
  if [[ -n "$KEY_VAULT_NAME" ]]; then
    if [[ -n "$SUBSCRIPTION_TENANT_ID" ]]; then
      echo "--> Switching context to the subscription's tenant ($SUBSCRIPTION_TENANT_ID) to write to Key Vault"
      az login --tenant "$SUBSCRIPTION_TENANT_ID" >/dev/null
    else
      echo "    (no --subscription-tenant-id given — assuming the current az context already has Key Vault access;"
      echo "     re-run 'az login' yourself first if that's not the case)"
    fi
    echo "--> Storing the client secret in Key Vault '$KEY_VAULT_NAME' as '$SECRET_NAME'"
    az keyvault secret set --vault-name "$KEY_VAULT_NAME" --name "$SECRET_NAME" --value "$GRAPH_CLIENT_SECRET" >/dev/null
    echo "    Stored. The secret is not printed below."
  else
    echo "    !! No --key-vault-name supplied. CAPTURE THIS SECRET NOW — it will not be shown again:"
    echo "    $GRAPH_CLIENT_SECRET"
  fi
else
  echo "--> Graph client secret: not generated by this run (existing app reused)."
  echo "    Store its already-issued value yourself — see step 4 comment above."
fi

# ------------------------------------------------------------------------------
# Summary — non-secret reference values for the Backend and React Engineers
# ------------------------------------------------------------------------------
cat <<SUMMARY

================================================================================
Entra reference values (non-secret — safe to share with Backend/React Engineers)
================================================================================
Entra tenant ID (sign-in, CIAM)  : $TENANT_ID
Graph/mail tenant ID             : $MAIL_TENANT_ID
SPA client ID                    : $SPA_APP_ID
SPA redirect URIs                : http://localhost:5173${WEB_APP_URL:+, $WEB_APP_URL}
API client ID                    : $API_APP_ID
API scope (SPA -> API)           : $API_SCOPE
Graph client ID (Workers only)   : $GRAPH_APP_ID
Graph permission granted         : Mail.ReadWrite (Application) — verify admin consent in portal
================================================================================
Reminder: if --web-app-url wasn't known yet when this ran, re-run the SPA
redirect PATCH once the App Service is deployed (back in the CIAM tenant):
  az login --tenant $TENANT_ID --allow-no-subscriptions
  az rest --method PATCH --uri https://graph.microsoft.com/v1.0/applications/$SPA_OBJECT_ID \
    --headers "Content-Type=application/json" \
    --body '{"spa":{"redirectUris":["http://localhost:5173","<final-web-app-url>"]}}'
================================================================================
SUMMARY
