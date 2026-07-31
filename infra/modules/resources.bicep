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
param entraAuthority string
param graphTenantId string
param graphClientId string
param graphMailboxUpn string

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
var docIntelName = 'docintel-${baseName}-${uniqueSuffix}'

var kvSecretsUserRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e6') // Key Vault Secrets User
var storageBlobDataContributorRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'ba92f5b4-2d11-453d-a403-e96b0029c9fe') // Storage Blob Data Contributor
var cognitiveServicesUserRoleId = subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'a97b65f3-24c7-4388-baec-2e87135dc908') // Cognitive Services User

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
// Azure AI Document Intelligence (WP-061) — S0 (Standard) tier. Real
// provisioning, closing the gap docs/Backlog.md flagged (this resource
// never existed, so APFlow.Api's own fail-fast startup check for
// DocumentIntelligence:Endpoint was blocking non-Development startup
// entirely, worked around live with a non-durable ASPNETCORE_ENVIRONMENT=
// Development override). `customSubDomainName` is REQUIRED here, not
// optional - Cognitive Services resources only support Azure AD/Managed
// Identity-based authentication (what this app actually uses, see the
// RBAC grant below) when a custom subdomain is assigned; without it, only
// API-key auth would work, silently breaking the DefaultAzureCredential
// path this app relies on.
// Location is deliberately hardcoded to 'uksouth', NOT the `location`
// param every other resource here uses ('ukwest') - confirmed live via
// `az cognitiveservices account list-skus --kind FormRecognizer`: ukwest
// returns zero SKUs (the account type is entirely unavailable there),
// uksouth returns both F0 and S0. Same UK-data-residency constraint as
// everywhere else in this template, just a different specific region
// within it - not the uksouth *compute* quota wall the `location` param's
// own doc comment describes (that was App Service Plan SKU capacity, an
// unrelated quota pool from this resource type).
// ----------------------------------------------------------------------------
resource docIntel 'Microsoft.CognitiveServices/accounts@2024-10-01' = {
  name: docIntelName
  location: 'uksouth'
  kind: 'FormRecognizer'
  sku: {
    name: 'S0'
  }
  properties: {
    customSubDomainName: docIntelName
    publicNetworkAccess: 'Enabled'
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
        // WP-068: AzureKeyVault:Enabled/VaultUri were never set as live app
        // settings at all, on top of KeyVaultOptions.Enabled defaulting to
        // false - Program.cs's AddAzureKeyVault call (and the explicit
        // graph-secret-{tenantId} -> Graph:ClientSecret mapping added
        // alongside it) had never executed this entire session. This is
        // what actually turns Key Vault loading on. Requires the API's
        // managed identity to hold Key Vault Secrets User on this vault -
        // already granted below (apiKvAccess, from WP-021).
        { name: 'AzureKeyVault__Enabled', value: 'true' }
        { name: 'AzureKeyVault__VaultUri', value: keyVault.properties.vaultUri }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'ENTRA_TENANT_ID', value: entraTenantId }
        { name: 'ENTRA_API_CLIENT_ID', value: entraApiClientId }
        { name: 'ENTRA_API_SCOPE', value: entraApiScope }
        // The three settings above are NOT read by any .NET code (confirmed
        // by grepping src/ for their literal names - zero matches) - kept
        // for handoff/documentation only, per the same reasoning as the
        // Graph/Storage values in docs/CI-CD-Pipeline.md's variables table.
        // AuthenticationExtensions.cs/EntraIdOptions actually bind the
        // "EntraId" config section, which needs the '__'-separated settings
        // below instead - these were missing entirely until this fix,
        // which is why AddApiAuthentication's own fail-fast check
        // ("EntraId:Authority and EntraId:Audience must be configured
        // outside Development") was throwing on every fresh container
        // start, live-diagnosed 2026-07-31 as exit code 134 (SIGABRT, the
        // Linux CoreCLR's unhandled-exception behavior) after this session's
        // Cors fix forced the API's first real restart in a while. Authority
        // uses the same tenant/subdomain as the frontend's ENTRA_AUTHORITY
        // GitHub variable, with the /v2.0 suffix ASP.NET Core's JWT bearer
        // Authority needs for signing-key discovery (confirmed live via
        // that URL's own /v2.0/.well-known/openid-configuration returning
        // 200). Audience was first set to the API app registration's
        // Application ID URI ('api://{clientId}') on the (reasonable but,
        // for this tenant type, wrong) assumption that a v2.0 token issued
        // for a custom "api://.../access_as_user" scope carries that URI as
        // its aud claim. Live-diagnosed 2026-07-31 by decoding a real,
        // rejected access token directly (IdentityModel redacts the actual
        // audience from its own IDX10214 exception message even with
        // ShowPII/Switch.DoNotScrubExceptions set - see git history on this
        // line for the AuthenticationExtensions.cs diagnostic that proved
        // it): Entra External ID (CIAM) issues the bare client ID as aud
        // for this scope shape, not the App ID URI - apparently a real
        // behavioral difference from classic Entra ID (Workforce) tenants,
        // not a misconfiguration on this app registration's side (its
        // identifierUris/requestedAccessTokenVersion are both already
        // correct, confirmed via `az ad app show`).
        { name: 'EntraId__Authority', value: '${entraAuthority}/v2.0' }
        { name: 'EntraId__Audience', value: entraApiClientId }
        // Same fail-fast-on-missing-config pattern as EntraId above, live-
        // diagnosed together during the same 2026-07-31 crash-loop
        // investigation: AddDatabase (Infrastructure/DependencyInjection.cs)
        // requires "ConnectionStrings:DefaultConnection", AddBlobStorage
        // requires "BlobStorage:ContainerName" + "BlobStorage:AccountUrl",
        // and AddGraph (Integrations/DependencyInjection.cs) requires
        // "Graph:TenantId"/"Graph:ClientId"/"Graph:MailboxUserPrincipalName"
        // - none of these matched any of the flat-named settings above
        // (SQL_SERVER_FQDN etc. are, like ENTRA_TENANT_ID, not read by any
        // .NET code). Graph:ClientSecret is deliberately left unset - it's
        // optional by design (GraphOptions.cs), falling back to this app's
        // own Managed Identity via DefaultAzureCredential, the preferred
        // secret-less path per that class's own doc comment.
        { name: 'ConnectionStrings__DefaultConnection', value: 'Server=tcp:${sqlServer.properties.fullyQualifiedDomainName},1433;Initial Catalog=${sqlDatabaseName};Authentication=Active Directory Default;Encrypt=True;TrustServerCertificate=False;' }
        { name: 'BlobStorage__ContainerName', value: blobContainerName }
        { name: 'BlobStorage__AccountUrl', value: storageAccount.properties.primaryEndpoints.blob }
        { name: 'Graph__TenantId', value: graphTenantId }
        { name: 'Graph__ClientId', value: graphClientId }
        { name: 'Graph__MailboxUserPrincipalName', value: graphMailboxUpn }
        // ASP.NET Core config binds ':' section separators to '__' in App
        // Service app settings, and array elements to a numeric suffix -
        // this is the live equivalent of appsettings.Development.json's
        // "Cors": { "AllowedOrigins": ["https://...web....azurewebsites.net"] }.
        // Confirmed missing live 2026-07-31 (az webapp config appsettings
        // list returned [] for this app) - WP-023's own flagged gap,
        // ConfigureCorsPolicy.cs fails closed (permits nothing) rather than
        // falling back to the Development-only permissive policy, so every
        // cross-origin request from the deployed web app was being
        // rejected. Uses the webAppServiceName *string variable*, not
        // webAppService.properties.defaultHostName - the latter would
        // create a circular resource dependency, since webAppService's own
        // API_BASE_URL setting already references apiAppService the other
        // way around.
        { name: 'Cors__AllowedOrigins__0', value: 'https://${webAppServiceName}.azurewebsites.net' }
        // WP-061: only the endpoint (non-secret) is wired into the running
        // app - DocumentIntelligenceOptions.ApiKey is deliberately left
        // unset, same shape as Graph:ClientSecret/BlobStorage's account-key
        // fallback, so auth goes through DefaultAzureCredential against the
        // "Cognitive Services User" RBAC grant below, not a stored key. The
        // key itself is stored in Key Vault separately, for reference/
        // break-glass only - see docs/Backlog.md and the WP-061 report.
        { name: 'DocumentIntelligence__Endpoint', value: docIntel.properties.endpoint }
        // WP-069: EmailIngestionWorker's background DI scopes have no HttpContext,
        // so WorkerCurrentUserService (not the real, HTTP-path CurrentUserService)
        // resolves for them, returning this value as the tenant it acts on behalf
        // of. Deliberately reuses entraTenantId rather than adding a second,
        // independent value - this MUST be the same tenant id real GB Skips users
        // actually sign in through (the CIAM tenant), not
        // WorkflowSeedData.GbSkipsPlaceholderTenantId, which is documented as never
        // matching any real caller. Using the wrong value here would make every
        // invoice the worker creates invisible to real users (TenantId mismatch
        // against AppDbContext's tenant query filter).
        { name: 'Workers__TenantId', value: entraTenantId }
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
      // src/APFlow.Web/server.js.
      appCommandLine: 'node server.js'
      appSettings: [
        { name: 'API_BASE_URL', value: 'https://${apiAppService.properties.defaultHostName}' }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsights.properties.ConnectionString }
        { name: 'ENTRA_TENANT_ID', value: entraTenantId }
        { name: 'ENTRA_SPA_CLIENT_ID', value: entraSpaClientId }
        { name: 'ENTRA_API_SCOPE', value: entraApiScope }
        // Explicitly false (Azure's own default for zip deploy, set here for
        // durability against that default ever changing): the CI artifact
        // already ships a pre-installed production node_modules, so no
        // post-deploy install is needed. Was 'true' first, relying on Oryx to
        // install node_modules - broke the deploy instead, because Oryx also
        // auto-runs package.json's "build" script ("tsc -b && vite build")
        // whenever one exists, and this artifact ships only the already-built
        // dist/, not tsconfig.json/src/ - producing
        // "TS5083: Cannot read file 'tsconfig.json'". See ci-cd.yml's
        // "Install production-only dependencies for deployment" step.
        { name: 'SCM_DO_BUILD_DURING_DEPLOYMENT', value: 'false' }
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

// WP-061: APFlow.Api only - APFlow.Web has no reason to call Document
// Intelligence directly, same least-privilege reasoning already applied to
// its SQL/Key Vault grants above.
resource apiDocIntelAccess 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(docIntel.id, apiAppService.id, cognitiveServicesUserRoleId)
  scope: docIntel
  properties: {
    roleDefinitionId: cognitiveServicesUserRoleId
    principalId: apiAppService.identity.principalId
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

resource docIntelDiagnostics 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'diag-${docIntelName}'
  scope: docIntel
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
output docIntelName string = docIntel.name
output docIntelEndpoint string = docIntel.properties.endpoint
