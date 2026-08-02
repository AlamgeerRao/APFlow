// Checked-in canonical parameter values for the `dev` environment's
// subscription-scope deployment (infra/main.bicep). None of these are
// secrets - tenant/client/object IDs and a UPN login, no credentials.
//
// Why this file exists: main.bicep's entraTenantId/entraSpaClientId/
// entraApiClientId/entraApiScope all default to '' so a brand-new
// environment can be bootstrapped before its Entra app registrations exist
// (see README "Deployment order" step 1, run before step 2). But for an
// EXISTING environment like this one, omitting them on a later redeploy
// would silently reset the already-working sign-in config back to blank -
// a real hazard flagged by the DevOps engineer reviewing the 2026-07-31
// server.js/Bicep redeploy (see docs/Backlog.md). Always deploy this
// environment via this file, not by re-typing --parameters by hand, so
// there is one durable source of truth instead of depending on someone
// remembering to query live App Service settings first.
//
// If any of these values ever change (e.g. Entra app registrations are
// recreated), update them here in the same commit as whatever changed them.
using 'main.bicep'

param environmentName = 'dev'
param location = 'ukwest'
param namePrefix = 'apflow'
param appServicePlanSku = 'B1'
param logRetentionDays = 30

// SQL Active Directory admin - subscription's own/home Entra tenant, not the
// CIAM tenant. Confirmed live via `az sql server ad-admin list`.
param sqlAadAdminObjectId = '4f09ded8-75dd-44f5-98f6-e9139a5b3701'
param sqlAadAdminLogin = 'alam@rameezjav.onmicrosoft.com'
param sqlAadAdminPrincipalType = 'User'

// Entra External ID (CIAM) dev tenant + app registrations, from Task 1
// (infra/scripts/create-entra-app-registrations.sh). Confirmed live via
// `az webapp config appsettings list` on both App Services, 2026-07-31.
param entraTenantId = '641fc267-7902-48d0-8e1c-1d3d0166c8ac'
param entraSpaClientId = 'd47fcb44-752e-4d7a-ac49-d3c71dfca7e0'
param entraApiClientId = '603682ec-46ab-4075-9e87-8e44478a39a4'
param entraApiScope = 'api://603682ec-46ab-4075-9e87-8e44478a39a4/access_as_user'
// Same tenant/subdomain as the frontend's ENTRA_AUTHORITY GitHub variable
// (no /v2.0 suffix here - resources.bicep appends that for APFlow.Api's
// JWT bearer Authority). Live-verified via
// https://rameezjav.ciamlogin.com/641fc267-7902-48d0-8e1c-1d3d0166c8ac/v2.0/.well-known/openid-configuration
// returning 200 with a matching tenant ID.
param entraAuthority = 'https://rameezjav.ciamlogin.com/641fc267-7902-48d0-8e1c-1d3d0166c8ac'

// Graph app registration + mailbox (WP-021d). NOT the same tenant as
// entraTenantId above - see GraphOptions.cs's remarks. Values already
// established as this environment's GRAPH_TENANT_ID/GRAPH_CLIENT_ID/
// GRAPH_MAILBOX_UPN GitHub variables (docs/CI-CD-Pipeline.md).
param graphTenantId = '1df7da13-5ab0-4a95-a11b-1f8bbd9c5fcf'
param graphClientId = '40d63c64-ff18-4028-ba92-01ca93c1c432'
param graphMailboxUpn = 'invoices@acoounts01.onmicrosoft.com'

// WP-024: alert notifications (application failures, database Unhealthy).
param alertEmail = 'raoalamgeer25@gmail.com'
