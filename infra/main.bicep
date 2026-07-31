// ============================================================================
// AP Flow — WP-021: Development Infrastructure (Identity + Core Azure Resources)
// Subscription-scope orchestrator: creates the Resource Group, then deploys
// all resources into it via modules/resources.bicep
//
// Scope: DEVELOPMENT ONLY. This template intentionally only allows
// environmentName = 'dev' — staging/prod are separate, future work packages.
// ============================================================================
targetScope = 'subscription'

@description('Environment name. Locked to dev for this work package — do not repurpose for staging/prod without a separate reviewed WP.')
@allowed([
  'dev'
])
param environmentName string = 'dev'

@description('Azure region for all resources. Defaults to ukwest — uksouth was tried first but hit a subscription compute quota wall (SubscriptionIsOverQuotaForSku) during actual deployment; ukwest had quota available and keeps data in the UK for compliance.')
param location string = 'ukwest'

@description('Short prefix used to build resource names')
param namePrefix string = 'apflow'

@description('SKU for the (single) App Service Plan hosting both App Services')
param appServicePlanSku string = 'B1'

@description('Object ID (in the Azure subscription\'s own/home Entra tenant — NOT the new Entra External ID CIAM tenant from Task 1) of the user or group to set as the Azure SQL Active Directory admin. SQL is deployed Azure-AD-only: there is no SQL login/password.')
param sqlAadAdminObjectId string

@description('Display name of the SQL AAD admin user or group, matching sqlAadAdminObjectId')
param sqlAadAdminLogin string

@description('Whether sqlAadAdminObjectId refers to a User or a Group')
@allowed([
  'User'
  'Group'
])
param sqlAadAdminPrincipalType string = 'User'

@description('Log Analytics Workspace retention in days')
param logRetentionDays int = 30

// ---------------------------------------------------------------------------
// Optional: Entra reference values, wired up as non-secret App Service settings
// once Task 1's app registration script has been run. Safe to leave blank on
// first deploy; re-run the deployment after populating them.
// ---------------------------------------------------------------------------
@description('Entra External ID (CIAM) dev tenant ID used for AP Flow sign-in (from Task 1). Leave blank until the tenant/app registrations exist.')
param entraTenantId string = ''

@description('Application (client) ID of the APFlow SPA app registration')
param entraSpaClientId string = ''

@description('Application (client) ID / Application ID URI of the APFlow.Api app registration (the API resource + Graph confidential client)')
param entraApiClientId string = ''

@description('The exposed API scope, e.g. api://<entraApiClientId>/access_as_user')
param entraApiScope string = ''

@description('The Entra External ID (CIAM) tenant authority base URL, e.g. https://<tenant-subdomain>.ciamlogin.com/<tenantId> (no trailing /v2.0 - resources.bicep appends that). Required by APFlow.Api\'s own JWT bearer authentication (EntraId:Authority) - unlike the other entra* values above, this is NOT optional in any deployed (non-Development) environment: AuthenticationExtensions.cs deliberately fails fast at startup if it and entraApiClientId-derived Audience are not both set.')
param entraAuthority string = ''

@description('The Microsoft 365 / Entra ID tenant hosting the mailbox APFlow.Api reads via Graph (Graph:TenantId - NOT the same tenant as entraTenantId, see GraphOptions.cs remarks). Same fail-fast-outside-Development requirement as entraAuthority above.')
param graphTenantId string = ''

@description('Application (client) ID of the Graph app registration (Graph:ClientId).')
param graphClientId string = ''

@description('The mailbox UPN/address APFlow.Api reads via Graph (Graph:MailboxUserPrincipalName).')
param graphMailboxUpn string = ''

var resourceGroupName = 'rg-${namePrefix}-${environmentName}'

resource rg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: resourceGroupName
  location: location
  tags: {
    project: 'ap-flow'
    environment: environmentName
    managedBy: 'bicep'
  }
}

module resources 'modules/resources.bicep' = {
  name: 'apflow-resources-${environmentName}'
  scope: rg
  params: {
    environmentName: environmentName
    location: location
    namePrefix: namePrefix
    appServicePlanSku: appServicePlanSku
    sqlAadAdminObjectId: sqlAadAdminObjectId
    sqlAadAdminLogin: sqlAadAdminLogin
    sqlAadAdminPrincipalType: sqlAadAdminPrincipalType
    logRetentionDays: logRetentionDays
    entraTenantId: entraTenantId
    entraSpaClientId: entraSpaClientId
    entraApiClientId: entraApiClientId
    entraApiScope: entraApiScope
    entraAuthority: entraAuthority
    graphTenantId: graphTenantId
    graphClientId: graphClientId
    graphMailboxUpn: graphMailboxUpn
  }
}

output resourceGroupName string = resourceGroupName
output apiAppServiceName string = resources.outputs.apiAppServiceName
output apiAppServiceUrl string = resources.outputs.apiAppServiceUrl
output webAppServiceName string = resources.outputs.webAppServiceName
output webAppServiceUrl string = resources.outputs.webAppServiceUrl
output keyVaultName string = resources.outputs.keyVaultName
output sqlServerFqdn string = resources.outputs.sqlServerFqdn
output sqlDatabaseName string = resources.outputs.sqlDatabaseName
output storageAccountName string = resources.outputs.storageAccountName
output blobContainerName string = resources.outputs.blobContainerName
output appInsightsName string = resources.outputs.appInsightsName
output logAnalyticsWorkspaceName string = resources.outputs.logAnalyticsWorkspaceName
output docIntelName string = resources.outputs.docIntelName
output docIntelEndpoint string = resources.outputs.docIntelEndpoint
