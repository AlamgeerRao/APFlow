-- ============================================================================
-- AP Flow — WP-022: Grant the CI/CD service principal database access to run
-- EF Core migrations against the Development database.
--
-- Same reasoning as WP-021's grant-sql-managed-identity-access.sql: this is a
-- T-SQL operation that must run against the database itself, authenticated
-- as the Azure AD admin — there's no ARM/Bicep resource type for it.
--
-- Rights granted: db_ddladmin (schema changes — what migrations are, by
-- definition) + db_datareader + db_datawriter (EF Core migrations can also
-- read/seed data). Deliberately NOT db_owner — narrower than WP-021's own
-- API app grant needed to be, since this identity's only job is running
-- migrations, not general application data access.
--
-- Prerequisites:
--   - infra/scripts/setup-github-oidc-service-principal.sh has already been run
--     (the CI/CD app registration + service principal exist).
--   - You are connecting AS the Azure AD admin (same one from WP-021), e.g.:
--       sqlcmd -S <sqlServerFqdn> -d <sqlDatabaseName> -G --authentication-method=ActiveDirectoryDefault
--     or the Azure Portal's Query Editor (Microsoft Entra Authentication) —
--     see WP-021's README for exactly how, if sqlcmd isn't available in your
--     Cloud Shell image.
--   - The user name below MUST exactly match the CI/CD app registration's
--     display name (APFlow-CI-Dev, unless renamed) — this is also the
--     display name of its Enterprise Application/service principal.
-- ============================================================================

DECLARE @ciAppDisplayName SYSNAME = N'APFlow-CI-Dev';

DECLARE @sql NVARCHAR(MAX);

SET @sql = N'
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = ''' + @ciAppDisplayName + N''')
BEGIN
    CREATE USER [' + @ciAppDisplayName + N'] FROM EXTERNAL PROVIDER;
END
ALTER ROLE db_ddladmin ADD MEMBER [' + @ciAppDisplayName + N'];
ALTER ROLE db_datareader ADD MEMBER [' + @ciAppDisplayName + N'];
ALTER ROLE db_datawriter ADD MEMBER [' + @ciAppDisplayName + N'];
';
EXEC sp_executesql @sql;

-- Verify
SELECT name, type_desc, authentication_type_desc
FROM sys.database_principals
WHERE name = @ciAppDisplayName;
