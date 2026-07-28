-- ============================================================================
-- AP Flow — WP-021: Grant App Service managed identities access to Azure SQL
--
-- Why this exists as a separate script: Bicep/ARM can create the SQL server
-- and database, but creating a *contained database user* for a managed
-- identity is a T-SQL operation that must run against the database itself,
-- authenticated as the Azure AD admin. There is no ARM resource type for this.
--
-- Prerequisites:
--   - infra/main.bicep has already been deployed (server + database + AAD-only
--     admin exist; both App Services exist with system-assigned identities).
--   - You are connecting AS the Azure AD admin configured in main.bicep
--     (sqlAadAdminObjectId / sqlAadAdminLogin), e.g. via:
--       sqlcmd -S <sqlServerFqdn> -d <sqlDatabaseName> -G --authentication-method=ActiveDirectoryDefault
--     or Azure Data Studio / SSMS with "Azure Active Directory - Universal / Default" auth.
--   - The user name below MUST exactly match each App Service's name (this is
--     also the display name of the Enterprise Application created for its
--     system-assigned managed identity). Replace the placeholders before running.
-- ============================================================================

-- Replace with the actual App Service names (see Bicep outputs: apiAppServiceName / webAppServiceName)
DECLARE @apiAppServiceName SYSNAME = N'<apiAppServiceName-from-bicep-output>';
DECLARE @webAppServiceName SYSNAME = N'<webAppServiceName-from-bicep-output>';

DECLARE @sql NVARCHAR(MAX);

-- APFlow.Api — needs full read/write for normal application operation
SET @sql = N'
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = ''' + @apiAppServiceName + N''')
BEGIN
    CREATE USER [' + @apiAppServiceName + N'] FROM EXTERNAL PROVIDER;
END
ALTER ROLE db_datareader ADD MEMBER [' + @apiAppServiceName + N'];
ALTER ROLE db_datawriter ADD MEMBER [' + @apiAppServiceName + N'];
';
EXEC sp_executesql @sql;

-- APFlow.Web — provisioned per this WP's explicit instruction that both App
-- Services get SQL access. In practice the SPA host has no server-side data
-- access pattern today; granting read-only here rather than read/write until
-- a concrete need is identified (flag for Chief Technical Architect review —
-- see README "Observations for review").
SET @sql = N'
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = ''' + @webAppServiceName + N''')
BEGIN
    CREATE USER [' + @webAppServiceName + N'] FROM EXTERNAL PROVIDER;
END
ALTER ROLE db_datareader ADD MEMBER [' + @webAppServiceName + N'];
';
EXEC sp_executesql @sql;

-- Verify
SELECT name, type_desc, authentication_type_desc
FROM sys.database_principals
WHERE name IN (@apiAppServiceName, @webAppServiceName);
