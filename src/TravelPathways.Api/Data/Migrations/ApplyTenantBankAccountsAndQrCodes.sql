-- Create TenantBankAccounts and TenantQrCodes tables (from migration AddTenantBankAccountsAndQrCodes).
-- Run this against your database if the migration hasn't been applied.

IF OBJECT_ID(N'dbo.TenantBankAccounts', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TenantBankAccounts] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [AccountHolderName] nvarchar(max) NOT NULL,
        [BankName] nvarchar(max) NOT NULL,
        [AccountNumber] nvarchar(max) NOT NULL,
        [IFSC] nvarchar(max) NOT NULL,
        [Branch] nvarchar(max) NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TenantBankAccounts] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantBankAccounts_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_TenantBankAccounts_TenantId] ON [dbo].[TenantBankAccounts] ([TenantId]);
END
GO

IF OBJECT_ID(N'dbo.TenantQrCodes', N'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[TenantQrCodes] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Label] nvarchar(max) NOT NULL,
        [ImageUrl] nvarchar(max) NOT NULL,
        [FileName] nvarchar(max) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TenantQrCodes] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantQrCodes_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_TenantQrCodes_TenantId] ON [dbo].[TenantQrCodes] ([TenantId]);
END
GO
