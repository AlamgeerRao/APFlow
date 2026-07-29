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
--   - The user name below MUST exactly match the App Service's name (this is
--     also the display name of the Enterprise Application created for its
--     system-assigned managed identity). Replace the placeholder before running.
-- ============================================================================

-- Replace with the actual App Service name (see Bicep output: apiAppServiceName)
DECLARE @apiAppServiceName SYSNAME = N'<apiAppServiceName-from-bicep-output>';

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

-- APFlow.Web (the SPA host) intentionally gets NO database user here.
-- Per Chief Technical Architect ruling: a static SPA host has no server-side
-- reason to touch SQL directly — WP-021's original "both App Services" grant
-- was over-provisioning per 02_Project_Standards.md §4 (least privilege) and
-- has been narrowed to APFlow.Api only.

-- Verify
SELECT name, type_desc, authentication_type_desc
FROM sys.database_principals
WHERE name = @apiAppServiceName;
