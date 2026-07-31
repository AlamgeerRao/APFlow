// ============================================================================
// AP Flow — WP-021: Development Infrastructure
// Resource-group-scope module.
//
// Passwordless by design:
//   - Azure SQL is deployed Azure-AD-only (no SQL login/password exists at all).
//     Both App Services authenticate to SQL as themselves via managed identity;
//     contained database users are created afterwards via the companion
//     scripts/grant-sql-managed-identity-access.sql (T-SQL cannot be run from
//     Bicep/ARM).
//   - Blob Storage access is via RBAC (Storage Blob Data Contributor) on the
//     App Services' managed identities — no storage account keys are issued
//     to the apps.
//   - Key Vault holds only what genuinely needs a secret going forward (e.g.
//     the Microsoft Graph app registration's client secret, added out-of-band
//     once Task 1's script has run) — never SQL or Storage credentials.
//   - No connection strings or keys appear directly in App Service config;
//     only non-secret identifiers (server FQDN, DB name, storage account
//     name/endpoint, Key Vault URI).
// ============================================================================
targetScope = 'resourceGroup'

param environmentName string
param location string
param namePrefix string
param appServicePlanSku string

param sqlAadAdminObjectId string
param sqlAadAdminLogin string
param sqlAadAdminPrincipalType string

param logRetentionDays int

param entraTenantId string
param entraSpaClientId string
param entraApiClientId string
param entraApiScope string

// Unique suffix keeps globally-unique names (storage, sql, key vault, app services) collision-free
var uniqueSuffix = uniqueString(resourceGroup().id, environmentName)
var baseName = '${namePrefix}-${environmentName}'

var appServicePlanName = 'plan-${baseName}'
var apiAppServiceName = 'app-${baseName}-api-${uniqueSuffix}'
var webAppServiceName = 'app-${baseName}-web-${uniqueSuffix}'
var sqlServerName = 'sql-${baseName}-${uniqueSuffix}'
var sqlDatabaseName = 'sqldb-${baseName}'
// NOTE: this name sits exactly at Azure's 24-character storage account limit
// with the default namePrefix/environmentName ('apflow'/'dev'). Lengthening
// either would push it over — no headroom left, flagged here for whoever
// changes those defaults later.
var storageAccountName = toLower('st${namePrefix}${environmentName}${uniqueSuffix}')
var blobContainerName = 'documents'
var keyVaultName = 'kv-${namePrefix}-${environmentName}-${substring(uniqueSuffix, 0, 6)}'
var logAnalyticsName = 'log-${baseName}-${uniqueSuffix}'
var appInsightsName = 'appi-${baseName}-${uniqueSuffix}'

var kvSecretsUserRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
var storageBlobDataContributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor

// ----------------------------------------------------------------------------
// Log Analytics + Application Insights (workspace-based)
// ----------------------------------------------------------------------------
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: logRetentionDays
  }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalytics.id
    IngestionMode: 'LogAnalytics'
  }
}

// ----------------------------------------------------------------------------
// Key Vault (RBAC authorization model — no access policies)
// ----------------------------------------------------------------------------
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
    enablePurgeProtection: true
  }
}

// ----------------------------------------------------------------------------
// Storage Account + Blob Container (no account keys handed to apps — RBAC only)
// ----------------------------------------------------------------------------
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false // forces Entra ID (RBAC) auth for data-plane access; no account keys usable
    supportsHttpsTrafficOnly: true
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  parent: storageAccount
  name: 'default'
}

resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  parent: blobService
  name: blobContainerName
  properties: {
    publicAccess: 'None'
  }
}

// ----------------------------------------------------------------------------
// Azure SQL Server (Azure-AD-only — no SQL login/password) + Database
// ----------------------------------------------------------------------------
resource sqlServer 'Microsoft.Sql/servers@2023-08-01-preview' = {
  name: sqlServerName
  location: location
  properties: {
    administrators: {
      administratorType: 'ActiveDirectory'
      principalType: sqlAadAdminPrincipalType
      login: sqlAadAdminLogin
      sid: sqlAadAdminObjectId
      tenantId: subscription().tenantId
      azureADOnlyAuthentication: true
    }
    minimalTlsVersion: '1.2'
    publicNetworkAccess: 'Enabled'
  }
}

// Single shared database for the current phase (confirmed, time-boxed position —
// see 03_Solution_Structure.md / Domain Reference Data notes on shared-vs-per-tenant).
// Schema is applied via EF Core migrations, never baked into this template.
resource sqlDatabase 'Microsoft.Sql/servers/databases@2023-08-01-preview' = {
  parent: sqlServer
  name: sqlDatabaseName
  location: location
  sku: {
    name: 'Basic'
    tier: 'Basic'
  }
  properties: {
    maxSizeBytes: 2147483648
  }
}

// Allows Azure services (the App Services) to reach the SQL server
resource sqlFirewallAllowAzure 'Microsoft.Sql/servers/firewallRules@2023-08-01-preview' = {
  parent: sqlServer
  name: 'AllowAzureServices'
  properties: {
    startIpAddress: '0.0.0.0'
    endIpAddress: '0.0.0.0'
  }
}

// ----------------------------------------------------------------------------
// App Service Plan (single plan, two apps) — Linux
// ----------------------------------------------------------------------------
resource appServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: appServicePlanSku
  }
  kind: 'linux'
  properties: {
    reserved: true
  }
}

// ----------------------------------------------------------------------------
// App Service: APFlow.Api — system-assigned identity, no secrets in config
// ----------------------------------------------------------------------------
resource apiAppService 'Microsoft.Web/sites@2023-12-01' = {
  name: apiAppServiceName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|9.0'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      appSettings: [
        { name: 'SQL_SERVER_FQDN', value: sqlServer.properties.fullyQualifiedDomainName }
        { name: 'SQL_DATABASE_NAME', value: sqlDatabaseName }
        { name: 'STORAGE_ACCOUNT_NAME', value: storageAccount.name }
        { name: 'STORAGE_BLOB_ENDPOINT', value: storageAccount.properties.primaryEndpoints.blob }
        { name: 'STORAGE_CONTAINER_NAME', value: blobContainerName }
        { name: 'KEY_VAULT_URI', value: keyVault.properties.vaultUri }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'ENTRA_TENANT_ID', value: entraTenantId }
        { name: 'ENTRA_API_CLIENT_ID', value: entraApiClientId }
        { name: 'ENTRA_API_SCOPE', value: entraApiScope }
      ]
    }
  }
}

// ----------------------------------------------------------------------------
// App Service: APFlow.Web — hosts the built React SPA on App Service
// (confirmed: App Service, not Static Web Apps). System-assigned identity
// provisioned per this WP's instruction; see README for a note on whether it
// actually needs the SQL/Key Vault grants below in practice.
// ----------------------------------------------------------------------------
resource webAppService 'Microsoft.Web/sites@2023-12-01' = {
  name: webAppServiceName
  location: location
  kind: 'app,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'NODE|24-lts'
      minTlsVersion: '1.2'
      ftpsState: 'Disabled'
      // Without an explicit startup command, App Service falls back to its own
      // generic static-file server (node /opt/startup/default-static-site.js),
      // which looks for content at wwwroot's root — but the CI artifact puts
      // the built SPA under wwwroot/dist/, served by this repo's own
      // src/APFlow.Web/server.js. SCM_DO_BUILD_DURING_DEPLOYMENT is required
      // because the artifact ships package.json/package-lock.json but not
      // node_modules — Oryx's post-deploy build step installs them.
      appCommandLine: 'node server.js'
      appSettings: [
        { name: 'API_BASE_URL', value: 'https://${apiAppService.properties.defaultHostName}' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'ENTRA_TENANT_ID', value: entraTenantId }
        { name: 'ENTRA_SPA_CLIENT_ID', value: entraSpaClientId }
        { name: 'ENTRA_API_SCOPE', value: entraApiScope }
        { name: 'SCM_DO_BUILD_DURING_DEPLOYMENT', value: 'true' }
      ]
    }
  }
}

// ----------------------------------------------------------------------------
// RBAC: both App Services' managed identities get Key Vault + Storage access
// (explicitly requested for both apps in this WP; see README for the
// least-privilege note on APFlow.Web's grants)
// ----------------------------------------------------------------------------
resource apiKvAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, apiAppService.id, kvSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: kvSecretsUserRoleId
    principalId: apiAppService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// APFlow.Web (the SPA host) intentionally has NO Key Vault or SQL grants.
// Per Chief Technical Architect ruling: a static SPA host has no server-side
// logic that would ever need either — granting them anyway would be
// over-provisioning against 02_Project_Standards.md §4's least-privilege
// principle for no corresponding benefit. (Storage access below is
// unaffected by this ruling and was not flagged.)

resource apiStorageAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, apiAppService.id, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: storageBlobDataContributorRoleId
    principalId: apiAppService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

resource webStorageAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccount.id, webAppService.id, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: storageBlobDataContributorRoleId
    principalId: webAppService.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Note: SQL access for both App Services' managed identities is granted via
// contained database users, which requires a T-SQL connection (AAD auth) and
// cannot be expressed in Bicep/ARM. See scripts/grant-sql-managed-identity-access.sql.

// ----------------------------------------------------------------------------
// Diagnostic settings → Log Analytics
// ----------------------------------------------------------------------------
resource apiDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-${apiAppServiceName}'
  scope: apiAppService
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      { categoryGroup: 'allLogs', enabled: true }
    ]
    metrics: [
      { category: 'AllMetrics', enabled: true }
    ]
  }
}

resource webDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-${webAppServiceName}'
  scope: webAppService
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      { categoryGroup: 'allLogs', enabled: true }
    ]
    metrics: [
      { category: 'AllMetrics', enabled: true }
    ]
  }
}

resource sqlDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-${sqlDatabaseName}'
  scope: sqlDatabase
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      { categoryGroup: 'allLogs', enabled: true }
    ]
    metrics: [
      { category: 'Basic', enabled: true }
    ]
  }
}

resource keyVaultDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-${keyVaultName}'
  scope: keyVault
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      { categoryGroup: 'allLogs', enabled: true }
    ]
    metrics: [
      { category: 'AllMetrics', enabled: true }
    ]
  }
}

resource blobDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-${storageAccountName}-blob'
  scope: blobService
  properties: {
    workspaceId: logAnalytics.id
    logs: [
      { categoryGroup: 'allLogs', enabled: true }
    ]
    metrics: [
      { category: 'Transaction', enabled: true }
    ]
  }
}

// ----------------------------------------------------------------------------
// Outputs
// ----------------------------------------------------------------------------
output apiAppServiceName string = apiAppService.name
output apiAppServiceUrl string = 'https://${apiAppService.properties.defaultHostName}'
output webAppServiceName string = webAppService.name
output webAppServiceUrl string = 'https://${webAppService.properties.defaultHostName}'
output keyVaultName string = keyVault.name
output sqlServerFqdn string = sqlServer.properties.fullyQualifiedDomainName
output sqlDatabaseName string = sqlDatabaseName
output storageAccountName string = storageAccount.name
output blobContainerName string = blobContainerName
output appInsightsName string = appInsights.name
output logAnalyticsWorkspaceName string = logAnalytics.name
