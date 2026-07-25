BEGIN TRANSACTION;
CREATE TABLE [InvoiceExtractedFields] (
    [Id] uniqueidentifier NOT NULL,
    [InvoiceId] uniqueidentifier NOT NULL,
    [FieldKey] nvarchar(100) NOT NULL,
    [Label] nvarchar(200) NOT NULL,
    [Value] nvarchar(4000) NULL,
    [ConfidenceScore] float NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAtUtc] datetimeoffset NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAtUtc] datetimeoffset NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InvoiceExtractedFields] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InvoiceExtractedFields_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE CASCADE
);

CREATE INDEX [IX_InvoiceExtractedFields_InvoiceId] ON [InvoiceExtractedFields] ([InvoiceId]);

CREATE INDEX [IX_InvoiceExtractedFields_TenantId_InvoiceId] ON [InvoiceExtractedFields] ([TenantId], [InvoiceId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260725111332_AddInvoiceExtractedFields', N'9.0.18');

COMMIT;
GO

