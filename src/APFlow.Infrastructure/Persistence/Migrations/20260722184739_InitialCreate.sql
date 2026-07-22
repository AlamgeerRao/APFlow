IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [AuditLogs] (
    [Id] uniqueidentifier NOT NULL,
    [Action] nvarchar(100) NOT NULL,
    [EntityName] nvarchar(100) NOT NULL,
    [EntityId] uniqueidentifier NOT NULL,
    [PreviousValue] nvarchar(2000) NULL,
    [NewValue] nvarchar(2000) NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAtUtc] datetimeoffset NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAtUtc] datetimeoffset NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_AuditLogs] PRIMARY KEY ([Id])
);

CREATE TABLE [Suppliers] (
    [Id] uniqueidentifier NOT NULL,
    [Name] nvarchar(256) NOT NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAtUtc] datetimeoffset NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAtUtc] datetimeoffset NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Suppliers] PRIMARY KEY ([Id])
);

CREATE TABLE [Invoices] (
    [Id] uniqueidentifier NOT NULL,
    [SupplierId] uniqueidentifier NOT NULL,
    [SupplierInvoiceNumber] nvarchar(128) NULL,
    [InvoiceDate] date NULL,
    [DueDate] date NULL,
    [Currency] nvarchar(3) NULL,
    [NetAmount] decimal(18,2) NULL,
    [Vat] decimal(18,2) NULL,
    [GrossTotal] decimal(18,2) NULL,
    [Status] nvarchar(32) NOT NULL,
    [SourceEmailMessageId] nvarchar(512) NULL,
    [SourceDocumentBlobName] nvarchar(1024) NULL,
    [IsPotentialDuplicate] bit NOT NULL DEFAULT CAST(0 AS bit),
    [DuplicateCheckReason] nvarchar(4000) NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAtUtc] datetimeoffset NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAtUtc] datetimeoffset NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_Invoices] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Invoices_Suppliers_SupplierId] FOREIGN KEY ([SupplierId]) REFERENCES [Suppliers] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [InvoiceNotes] (
    [Id] uniqueidentifier NOT NULL,
    [InvoiceId] uniqueidentifier NOT NULL,
    [Content] nvarchar(4000) NOT NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAtUtc] datetimeoffset NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAtUtc] datetimeoffset NULL,
    [DeletedBy] nvarchar(max) NULL,
    [TenantId] uniqueidentifier NOT NULL,
    CONSTRAINT [PK_InvoiceNotes] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_InvoiceNotes_Invoices_InvoiceId] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoices] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_AuditLogs_TenantId_EntityName_EntityId] ON [AuditLogs] ([TenantId], [EntityName], [EntityId]);

CREATE INDEX [IX_InvoiceNotes_InvoiceId] ON [InvoiceNotes] ([InvoiceId]);

CREATE INDEX [IX_InvoiceNotes_TenantId_InvoiceId] ON [InvoiceNotes] ([TenantId], [InvoiceId]);

CREATE INDEX [IX_Invoices_SupplierId] ON [Invoices] ([SupplierId]);

CREATE INDEX [IX_Invoices_TenantId_InvoiceDate] ON [Invoices] ([TenantId], [InvoiceDate]);

CREATE INDEX [IX_Invoices_TenantId_Status] ON [Invoices] ([TenantId], [Status]);

CREATE INDEX [IX_Invoices_TenantId_SupplierId] ON [Invoices] ([TenantId], [SupplierId]);

CREATE INDEX [IX_Suppliers_TenantId_Name] ON [Suppliers] ([TenantId], [Name]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260722184739_InitialCreate', N'9.0.0');

COMMIT;
GO

