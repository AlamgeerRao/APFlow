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
CREATE TABLE [ApprovalPolicies] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [Domain] nvarchar(100) NOT NULL,
    [RequiredRole] nvarchar(100) NOT NULL,
    [RequiresDualControl] bit NOT NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAtUtc] datetimeoffset NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAtUtc] datetimeoffset NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_ApprovalPolicies] PRIMARY KEY ([Id])
);

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

CREATE TABLE [WorkflowTemplates] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [DomainName] nvarchar(100) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAtUtc] datetimeoffset NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAtUtc] datetimeoffset NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_WorkflowTemplates] PRIMARY KEY ([Id])
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
    [Status] nvarchar(64) NOT NULL,
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

CREATE TABLE [StatusReferences] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [WorkflowTemplateId] uniqueidentifier NOT NULL,
    [Code] nvarchar(64) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [IsTerminal] bit NOT NULL,
    [SortOrder] int NOT NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAtUtc] datetimeoffset NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAtUtc] datetimeoffset NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_StatusReferences] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_StatusReferences_WorkflowTemplates_WorkflowTemplateId] FOREIGN KEY ([WorkflowTemplateId]) REFERENCES [WorkflowTemplates] ([Id]) ON DELETE NO ACTION
);

CREATE TABLE [WorkflowTransitions] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NULL,
    [WorkflowTemplateId] uniqueidentifier NOT NULL,
    [FromStatusCode] nvarchar(64) NOT NULL,
    [ToStatusCode] nvarchar(64) NOT NULL,
    [CreatedAtUtc] datetimeoffset NOT NULL,
    [CreatedBy] nvarchar(max) NULL,
    [ModifiedAtUtc] datetimeoffset NULL,
    [ModifiedBy] nvarchar(max) NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAtUtc] datetimeoffset NULL,
    [DeletedBy] nvarchar(max) NULL,
    CONSTRAINT [PK_WorkflowTransitions] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_WorkflowTransitions_WorkflowTemplates_WorkflowTemplateId] FOREIGN KEY ([WorkflowTemplateId]) REFERENCES [WorkflowTemplates] ([Id]) ON DELETE NO ACTION
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

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'DeletedAtUtc', N'DeletedBy', N'Domain', N'IsDeleted', N'ModifiedAtUtc', N'ModifiedBy', N'RequiredRole', N'RequiresDualControl', N'TenantId') AND [object_id] = OBJECT_ID(N'[ApprovalPolicies]'))
    SET IDENTITY_INSERT [ApprovalPolicies] ON;
INSERT INTO [ApprovalPolicies] ([Id], [CreatedAtUtc], [CreatedBy], [DeletedAtUtc], [DeletedBy], [Domain], [IsDeleted], [ModifiedAtUtc], [ModifiedBy], [RequiredRole], [RequiresDualControl], [TenantId])
VALUES ('00000000-0000-0000-0004-000000000001', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, N'InvoiceApproval', CAST(0 AS bit), NULL, NULL, N'FINANCE_MANAGER', CAST(0 AS bit), '00000000-0000-0000-0000-0000000b5121');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'DeletedAtUtc', N'DeletedBy', N'Domain', N'IsDeleted', N'ModifiedAtUtc', N'ModifiedBy', N'RequiredRole', N'RequiresDualControl', N'TenantId') AND [object_id] = OBJECT_ID(N'[ApprovalPolicies]'))
    SET IDENTITY_INSERT [ApprovalPolicies] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'DeletedAtUtc', N'DeletedBy', N'DomainName', N'IsDeleted', N'ModifiedAtUtc', N'ModifiedBy', N'Name', N'TenantId') AND [object_id] = OBJECT_ID(N'[WorkflowTemplates]'))
    SET IDENTITY_INSERT [WorkflowTemplates] ON;
INSERT INTO [WorkflowTemplates] ([Id], [CreatedAtUtc], [CreatedBy], [DeletedAtUtc], [DeletedBy], [DomainName], [IsDeleted], [ModifiedAtUtc], [ModifiedBy], [Name], [TenantId])
VALUES ('00000000-0000-0000-0001-000000000001', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, N'Invoice', CAST(0 AS bit), NULL, NULL, N'Platform Default', NULL),
('00000000-0000-0000-0001-000000000002', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, N'Invoice', CAST(0 AS bit), NULL, NULL, N'GB Skips Invoice Workflow', '00000000-0000-0000-0000-0000000b5121');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'DeletedAtUtc', N'DeletedBy', N'DomainName', N'IsDeleted', N'ModifiedAtUtc', N'ModifiedBy', N'Name', N'TenantId') AND [object_id] = OBJECT_ID(N'[WorkflowTemplates]'))
    SET IDENTITY_INSERT [WorkflowTemplates] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAtUtc', N'CreatedBy', N'DeletedAtUtc', N'DeletedBy', N'IsDeleted', N'IsTerminal', N'ModifiedAtUtc', N'ModifiedBy', N'Name', N'SortOrder', N'TenantId', N'WorkflowTemplateId') AND [object_id] = OBJECT_ID(N'[StatusReferences]'))
    SET IDENTITY_INSERT [StatusReferences] ON;
INSERT INTO [StatusReferences] ([Id], [Code], [CreatedAtUtc], [CreatedBy], [DeletedAtUtc], [DeletedBy], [IsDeleted], [IsTerminal], [ModifiedAtUtc], [ModifiedBy], [Name], [SortOrder], [TenantId], [WorkflowTemplateId])
VALUES ('00000000-0000-0000-0002-000000000001', N'RECEIVED', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Received', 10, NULL, '00000000-0000-0000-0001-000000000001'),
('00000000-0000-0000-0002-000000000002', N'EXTRACTED', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Extracted', 15, NULL, '00000000-0000-0000-0001-000000000001'),
('00000000-0000-0000-0002-000000000003', N'PROCESSING', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Processing', 20, NULL, '00000000-0000-0000-0001-000000000001'),
('00000000-0000-0000-0002-000000000004', N'DUPLICATE_SUSPECTED', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Duplicate Suspected', 30, NULL, '00000000-0000-0000-0001-000000000001'),
('00000000-0000-0000-0002-000000000005', N'AWAITING_REVIEW', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Awaiting Review', 40, NULL, '00000000-0000-0000-0001-000000000001'),
('00000000-0000-0000-0002-000000000006', N'NEEDS_QUERY', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Needs Query', 50, NULL, '00000000-0000-0000-0001-000000000001'),
('00000000-0000-0000-0002-000000000007', N'QUERY_RAISED', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Query Raised', 60, NULL, '00000000-0000-0000-0001-000000000001'),
('00000000-0000-0000-0002-000000000008', N'AWAITING_SUPPLIER_RESPONSE', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Awaiting Supplier Response', 70, NULL, '00000000-0000-0000-0001-000000000001'),
('00000000-0000-0000-0002-000000000009', N'APPROVED', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Approved', 80, NULL, '00000000-0000-0000-0001-000000000001'),
('00000000-0000-0000-0002-00000000000a', N'REJECTED', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Rejected', 90, NULL, '00000000-0000-0000-0001-000000000001'),
('00000000-0000-0000-0002-00000000000b', N'CANCELLED', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Cancelled', 100, NULL, '00000000-0000-0000-0001-000000000001'),
('00000000-0000-0000-0002-00000000000c', N'READY_FOR_PAYMENT', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Ready for Payment', 110, NULL, '00000000-0000-0000-0001-000000000001'),
('00000000-0000-0000-0002-00000000000d', N'PAID', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Paid', 120, NULL, '00000000-0000-0000-0001-000000000001'),
('00000000-0000-0000-0002-00000000000e', N'ARCHIVED', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(1 AS bit), NULL, NULL, N'Archived', 130, NULL, '00000000-0000-0000-0001-000000000001'),
('00000000-0000-0000-0003-000000000001', N'RECEIVED', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Received', 10, '00000000-0000-0000-0000-0000000b5121', '00000000-0000-0000-0001-000000000002'),
('00000000-0000-0000-0003-000000000002', N'EXTRACTED', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Extracted', 15, '00000000-0000-0000-0000-0000000b5121', '00000000-0000-0000-0001-000000000002'),
('00000000-0000-0000-0003-000000000003', N'PROCESSING', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Processing', 20, '00000000-0000-0000-0000-0000000b5121', '00000000-0000-0000-0001-000000000002'),
('00000000-0000-0000-0003-000000000004', N'DUPLICATE_SUSPECTED', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Duplicate Suspected', 30, '00000000-0000-0000-0000-0000000b5121', '00000000-0000-0000-0001-000000000002'),
('00000000-0000-0000-0003-000000000005', N'AWAITING_REVIEW', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Awaiting Review', 40, '00000000-0000-0000-0000-0000000b5121', '00000000-0000-0000-0001-000000000002'),
('00000000-0000-0000-0003-000000000006', N'CHECKED_READY_TO_APPROVE', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Checked & Ready to Approve', 45, '00000000-0000-0000-0000-0000000b5121', '00000000-0000-0000-0001-000000000002'),
('00000000-0000-0000-0003-000000000007', N'NEEDS_REVIEW_FEBINA', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Needs Review by Febina', 46, '00000000-0000-0000-0000-0000000b5121', '00000000-0000-0000-0001-000000000002'),
('00000000-0000-0000-0003-000000000008', N'NEEDS_QUERY', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Needs Query', 50, '00000000-0000-0000-0000-0000000b5121', '00000000-0000-0000-0001-000000000002'),
('00000000-0000-0000-0003-000000000009', N'QUERY_RAISED', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Query Raised', 60, '00000000-0000-0000-0000-0000000b5121', '00000000-0000-0000-0001-000000000002'),
('00000000-0000-0000-0003-00000000000a', N'AWAITING_SUPPLIER_RESPONSE', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Awaiting Supplier Response', 70, '00000000-0000-0000-0000-0000000b5121', '00000000-0000-0000-0001-000000000002'),
('00000000-0000-0000-0003-00000000000b', N'APPROVED', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Approved', 80, '00000000-0000-0000-0000-0000000b5121', '00000000-0000-0000-0001-000000000002'),
('00000000-0000-0000-0003-00000000000c', N'REJECTED', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Rejected', 90, '00000000-0000-0000-0000-0000000b5121', '00000000-0000-0000-0001-000000000002'),
('00000000-0000-0000-0003-00000000000d', N'CANCELLED', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Cancelled', 100, '00000000-0000-0000-0000-0000000b5121', '00000000-0000-0000-0001-000000000002'),
('00000000-0000-0000-0003-00000000000e', N'READY_FOR_PAYMENT', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Ready for Payment', 110, '00000000-0000-0000-0000-0000000b5121', '00000000-0000-0000-0001-000000000002'),
('00000000-0000-0000-0003-00000000000f', N'PAID', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(0 AS bit), NULL, NULL, N'Paid', 120, '00000000-0000-0000-0000-0000000b5121', '00000000-0000-0000-0001-000000000002'),
('00000000-0000-0000-0003-000000000010', N'ARCHIVED', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, CAST(0 AS bit), CAST(1 AS bit), NULL, NULL, N'Archived', 130, '00000000-0000-0000-0000-0000000b5121', '00000000-0000-0000-0001-000000000002');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'Code', N'CreatedAtUtc', N'CreatedBy', N'DeletedAtUtc', N'DeletedBy', N'IsDeleted', N'IsTerminal', N'ModifiedAtUtc', N'ModifiedBy', N'Name', N'SortOrder', N'TenantId', N'WorkflowTemplateId') AND [object_id] = OBJECT_ID(N'[StatusReferences]'))
    SET IDENTITY_INSERT [StatusReferences] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'DeletedAtUtc', N'DeletedBy', N'FromStatusCode', N'IsDeleted', N'ModifiedAtUtc', N'ModifiedBy', N'TenantId', N'ToStatusCode', N'WorkflowTemplateId') AND [object_id] = OBJECT_ID(N'[WorkflowTransitions]'))
    SET IDENTITY_INSERT [WorkflowTransitions] ON;
INSERT INTO [WorkflowTransitions] ([Id], [CreatedAtUtc], [CreatedBy], [DeletedAtUtc], [DeletedBy], [FromStatusCode], [IsDeleted], [ModifiedAtUtc], [ModifiedBy], [TenantId], [ToStatusCode], [WorkflowTemplateId])
VALUES ('00000000-0000-0000-0005-000000000001', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, N'CHECKED_READY_TO_APPROVE', CAST(0 AS bit), NULL, NULL, '00000000-0000-0000-0000-0000000b5121', N'APPROVED', '00000000-0000-0000-0001-000000000002');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'DeletedAtUtc', N'DeletedBy', N'FromStatusCode', N'IsDeleted', N'ModifiedAtUtc', N'ModifiedBy', N'TenantId', N'ToStatusCode', N'WorkflowTemplateId') AND [object_id] = OBJECT_ID(N'[WorkflowTransitions]'))
    SET IDENTITY_INSERT [WorkflowTransitions] OFF;

CREATE UNIQUE INDEX [IX_ApprovalPolicies_Domain_TenantId] ON [ApprovalPolicies] ([Domain], [TenantId]) WHERE [TenantId] IS NOT NULL;

CREATE INDEX [IX_AuditLogs_TenantId_EntityName_EntityId] ON [AuditLogs] ([TenantId], [EntityName], [EntityId]);

CREATE INDEX [IX_InvoiceNotes_InvoiceId] ON [InvoiceNotes] ([InvoiceId]);

CREATE INDEX [IX_InvoiceNotes_TenantId_InvoiceId] ON [InvoiceNotes] ([TenantId], [InvoiceId]);

CREATE INDEX [IX_Invoices_SupplierId] ON [Invoices] ([SupplierId]);

CREATE INDEX [IX_Invoices_TenantId_InvoiceDate] ON [Invoices] ([TenantId], [InvoiceDate]);

CREATE INDEX [IX_Invoices_TenantId_Status] ON [Invoices] ([TenantId], [Status]);

CREATE INDEX [IX_Invoices_TenantId_SupplierId] ON [Invoices] ([TenantId], [SupplierId]);

CREATE UNIQUE INDEX [IX_StatusReferences_WorkflowTemplateId_Code] ON [StatusReferences] ([WorkflowTemplateId], [Code]);

CREATE INDEX [IX_Suppliers_TenantId_Name] ON [Suppliers] ([TenantId], [Name]);

CREATE UNIQUE INDEX [IX_WorkflowTemplates_DomainName_TenantId] ON [WorkflowTemplates] ([DomainName], [TenantId]) WHERE [TenantId] IS NOT NULL;

CREATE UNIQUE INDEX [IX_WorkflowTransitions_WorkflowTemplateId_FromStatusCode_ToStatusCode] ON [WorkflowTransitions] ([WorkflowTemplateId], [FromStatusCode], [ToStatusCode]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260723145050_InitialCreate', N'9.0.0');

COMMIT;
GO

