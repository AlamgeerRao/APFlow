BEGIN TRANSACTION;
ALTER TABLE [InvoiceNotes] ADD [AuthorDisplayName] nvarchar(200) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260725014832_AddInvoiceNoteAuthorDisplayName', N'9.0.18');

COMMIT;
GO

