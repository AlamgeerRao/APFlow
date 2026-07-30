#!/usr/bin/env bash
# ==============================================================================
# AP Flow — WP-023 Task 2: Verify Cors:AllowedOrigins is live, not just correct
# in appsettings.Development.json in the repo.
#
# IMPORTANT — there are TWO SEPARATE CORS MECHANISMS on Azure App Service,
# easy to confuse, and this script checks both because which one AP Flow's
# API actually uses could not be confirmed against the real repo while
# building this script (the Filesystem connector used to read
# C:\Development\APFlow became unresponsive mid-session — see the WP-023
# report). Confirm which mechanism Program.cs actually wires up before
# treating either check alone as sufficient:
#
#   (a) APPLICATION-LEVEL CORS — a CorsPolicy built from a config value
#       (commonly bound from a "Cors:AllowedOrigins" section) inside
#       Program.cs/Startup, applied via app.UseCors(...). This is what the
#       WP's own wording ("Cors:AllowedOrigins") implies. Verified by
#       checking the app setting AND making a live cross-origin request.
#   (b) APP SERVICE PLATFORM CORS — a completely separate, code-independent
#       feature under the App Service resource itself (the "CORS" blade in
#       the Portal / `az webapp cors`). This does NOT read
#       Cors:AllowedOrigins and is unrelated to the app's own code. If the
#       app doesn't implement its own CORS middleware, don't assume this
#       platform feature is doing the job unless it's explicitly configured.
#
# Usage:
#   ./verify-cors-live.sh \
#     --resource-group rg-apflow-dev \
#     --api-app-service-name app-apflow-dev-api-ryd3y6fyfloxu \
#     --expected-origin https://app-apflow-dev-web-ryd3y6fyfloxu.azurewebsites.net
# ==============================================================================
set -euo pipefail

RESOURCE_GROUP=""
API_APP_SERVICE_NAME=""
EXPECTED_ORIGIN=""

usage() {
  echo "Usage: $0 --resource-group <rg> --api-app-service-name <name> --expected-origin <origin-url>"
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --resource-group) RESOURCE_GROUP="$2"; shift 2 ;;
    --api-app-service-name) API_APP_SERVICE_NAME="$2"; shift 2 ;;
    --expected-origin) EXPECTED_ORIGIN="$2"; shift 2 ;;
    *) usage ;;
  esac
done

[[ -z "$RESOURCE_GROUP" || -z "$API_APP_SERVICE_NAME" || -z "$EXPECTED_ORIGIN" ]] && usage

API_URL="https://$(az webapp show -g "$RESOURCE_GROUP" -n "$API_APP_SERVICE_NAME" --query defaultHostName -o tsv)"

echo "== AP Flow CORS live verification =="
echo "API App Service : $API_APP_SERVICE_NAME"
echo "API URL         : $API_URL"
echo "Expected origin : $EXPECTED_ORIGIN"
echo ""

# ------------------------------------------------------------------------------
# Check (a): application-level config — is Cors__AllowedOrigins actually
# present in the DEPLOYED app settings, not just the repo's
# appsettings.Development.json?
# ------------------------------------------------------------------------------
echo "--- Check 1: Cors__AllowedOrigins in the deployed App Service's app settings ---"
echo "(App Service maps a ':' in config keys to '__' in app settings — this is"
echo " the live equivalent of appsettings.json's \"Cors\": { \"AllowedOrigins\": [...] })"
az webapp config appsettings list \
  --resource-group "$RESOURCE_GROUP" \
  --name "$API_APP_SERVICE_NAME" \
  --query "[?starts_with(name, 'Cors')]" \
  -o table
echo ""
echo "If the above is empty: the app setting was never deployed, regardless of"
echo "what appsettings.Development.json in the repo says — that file is only"
echo "used when ASPNETCORE_ENVIRONMENT=Development AND no app setting overrides"
echo "it. Fix by explicitly setting it:"
echo "  az webapp config appsettings set -g $RESOURCE_GROUP -n $API_APP_SERVICE_NAME --settings \"Cors__AllowedOrigins__0=$EXPECTED_ORIGIN\""
echo ""

# ------------------------------------------------------------------------------
# Check (b): App Service's own separate platform CORS feature — checked
# for completeness, but do not treat this as equivalent to check (a).
# ------------------------------------------------------------------------------
echo "--- Check 2 (informational): App Service platform CORS feature (separate from app code) ---"
az webapp cors show --resource-group "$RESOURCE_GROUP" --name "$API_APP_SERVICE_NAME" -o table 2>/dev/null || echo "(none configured — expected, if the app implements its own CORS middleware instead)"
echo ""

# ------------------------------------------------------------------------------
# Check 3: an actual live cross-origin request — the definitive test,
# independent of which mechanism is in play. A real browser preflight is
# an OPTIONS request; this simulates one.
# ------------------------------------------------------------------------------
echo "--- Check 3: live preflight request test ---"
echo "Sending an OPTIONS request with Origin: $EXPECTED_ORIGIN ..."
RESPONSE_HEADERS=$(curl -s -D - -o /dev/null -X OPTIONS "$API_URL" \
  -H "Origin: $EXPECTED_ORIGIN" \
  -H "Access-Control-Request-Method: GET")
echo "$RESPONSE_HEADERS"
echo ""
if echo "$RESPONSE_HEADERS" | grep -qi "Access-Control-Allow-Origin: $EXPECTED_ORIGIN"; then
  echo "PASS — Access-Control-Allow-Origin echoed the expected origin. CORS is live."
elif echo "$RESPONSE_HEADERS" | grep -qi "Access-Control-Allow-Origin: \*"; then
  echo "PASS (but loose) — the API allows ANY origin (wildcard). Confirm this is"
  echo "actually intended for an authenticated API — usually it should not be."
else
  echo "FAIL — no matching Access-Control-Allow-Origin header was returned."
  echo "The API is not currently configured to accept requests from"
  echo "$EXPECTED_ORIGIN in a browser, regardless of what the repo says."
fi
