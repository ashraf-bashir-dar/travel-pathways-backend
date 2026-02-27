-- Create EmployeeSalary table (compensation/salary records per employee).
-- Run this if the table does not exist. Safe to run: skips if table already exists.

IF OBJECT_ID(N'dbo.EmployeeSalary', N'U') IS NOT NULL
  RETURN;

CREATE TABLE [dbo].[EmployeeSalary] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAtUtc] datetime2 NULL,
    [UserId] uniqueidentifier NOT NULL,
    [Type] nvarchar(max) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [PeriodLabel] nvarchar(max) NULL,
    [PaidOn] datetime2 NULL,
    [Notes] nvarchar(max) NULL,
    CONSTRAINT [PK_EmployeeSalary] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_EmployeeSalary_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_EmployeeSalary_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_EmployeeSalary_TenantId] ON [dbo].[EmployeeSalary] ([TenantId]);
CREATE INDEX [IX_EmployeeSalary_UserId] ON [dbo].[EmployeeSalary] ([UserId]);
GO
