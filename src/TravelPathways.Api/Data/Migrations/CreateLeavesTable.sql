-- Create Leaves table (employee leave requests; admin approve/reject).
-- Safe to run: skips if table already exists.

IF OBJECT_ID(N'dbo.Leaves', N'U') IS NOT NULL
  RETURN;

CREATE TABLE [dbo].[Leaves] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAtUtc] datetime2 NULL,
    [UserId] uniqueidentifier NOT NULL,
    [LeaveType] int NOT NULL,
    [StartDate] date NOT NULL,
    [EndDate] date NOT NULL,
    [Reason] nvarchar(max) NOT NULL,
    [Status] int NOT NULL,
    CONSTRAINT [PK_Leaves] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Leaves_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Leaves_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_Leaves_TenantId] ON [dbo].[Leaves] ([TenantId]);
CREATE INDEX [IX_Leaves_UserId] ON [dbo].[Leaves] ([UserId]);
CREATE INDEX [IX_Leaves_Status] ON [dbo].[Leaves] ([Status]);
CREATE INDEX [IX_Leaves_StartDate] ON [dbo].[Leaves] ([StartDate]);
GO
