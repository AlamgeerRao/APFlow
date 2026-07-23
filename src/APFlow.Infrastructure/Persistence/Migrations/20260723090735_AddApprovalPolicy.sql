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

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'DeletedAtUtc', N'DeletedBy', N'Domain', N'IsDeleted', N'ModifiedAtUtc', N'ModifiedBy', N'RequiredRole', N'RequiresDualControl', N'TenantId') AND [object_id] = OBJECT_ID(N'[ApprovalPolicies]'))
    SET IDENTITY_INSERT [ApprovalPolicies] ON;
INSERT INTO [ApprovalPolicies] ([Id], [CreatedAtUtc], [CreatedBy], [DeletedAtUtc], [DeletedBy], [Domain], [IsDeleted], [ModifiedAtUtc], [ModifiedBy], [RequiredRole], [RequiresDualControl], [TenantId])
VALUES ('00000000-0000-0000-0004-000000000001', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, N'InvoiceApproval', CAST(0 AS bit), NULL, NULL, N'FINANCE_MANAGER', CAST(0 AS bit), '00000000-0000-0000-0000-0000000b5121');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'DeletedAtUtc', N'DeletedBy', N'Domain', N'IsDeleted', N'ModifiedAtUtc', N'ModifiedBy', N'RequiredRole', N'RequiresDualControl', N'TenantId') AND [object_id] = OBJECT_ID(N'[ApprovalPolicies]'))
    SET IDENTITY_INSERT [ApprovalPolicies] OFF;

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'DeletedAtUtc', N'DeletedBy', N'FromStatusCode', N'IsDeleted', N'ModifiedAtUtc', N'ModifiedBy', N'TenantId', N'ToStatusCode', N'WorkflowTemplateId') AND [object_id] = OBJECT_ID(N'[WorkflowTransitions]'))
    SET IDENTITY_INSERT [WorkflowTransitions] ON;
INSERT INTO [WorkflowTransitions] ([Id], [CreatedAtUtc], [CreatedBy], [DeletedAtUtc], [DeletedBy], [FromStatusCode], [IsDeleted], [ModifiedAtUtc], [ModifiedBy], [TenantId], [ToStatusCode], [WorkflowTemplateId])
VALUES ('00000000-0000-0000-0005-000000000001', '2026-07-23T00:00:00.0000000+00:00', N'system', NULL, NULL, N'CHECKED_READY_TO_APPROVE', CAST(0 AS bit), NULL, NULL, '00000000-0000-0000-0000-0000000b5121', N'APPROVED', '00000000-0000-0000-0001-000000000002');
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAtUtc', N'CreatedBy', N'DeletedAtUtc', N'DeletedBy', N'FromStatusCode', N'IsDeleted', N'ModifiedAtUtc', N'ModifiedBy', N'TenantId', N'ToStatusCode', N'WorkflowTemplateId') AND [object_id] = OBJECT_ID(N'[WorkflowTransitions]'))
    SET IDENTITY_INSERT [WorkflowTransitions] OFF;

CREATE UNIQUE INDEX [IX_ApprovalPolicies_Domain_TenantId] ON [ApprovalPolicies] ([Domain], [TenantId]) WHERE [TenantId] IS NOT NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260723090735_AddApprovalPolicy', N'9.0.0');

COMMIT;
GO

