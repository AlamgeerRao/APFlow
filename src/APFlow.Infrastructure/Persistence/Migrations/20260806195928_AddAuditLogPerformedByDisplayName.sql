BEGIN TRANSACTION;
ALTER TABLE [AuditLogs] ADD [PerformedByDisplayName] nvarchar(200) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260806195928_AddAuditLogPerformedByDisplayName', N'9.0.18');

COMMIT;
GO

