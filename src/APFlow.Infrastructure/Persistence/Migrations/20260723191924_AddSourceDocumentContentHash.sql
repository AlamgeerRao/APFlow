BEGIN TRANSACTION;
ALTER TABLE [Invoices] ADD [SourceDocumentContentHash] nvarchar(64) NULL;

CREATE INDEX [IX_Invoices_TenantId_SourceDocumentContentHash] ON [Invoices] ([TenantId], [SourceDocumentContentHash]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260723191924_AddSourceDocumentContentHash', N'9.0.0');

COMMIT;
GO

