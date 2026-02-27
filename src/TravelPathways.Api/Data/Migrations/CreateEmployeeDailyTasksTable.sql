-- Create Tasks table (daily tasks per user for TimeSheet).
-- Run this if the table does not exist. Safe to run: skips if table already exists.

IF OBJECT_ID(N'dbo.Tasks', N'U') IS NOT NULL
  RETURN;

CREATE TABLE [dbo].[Tasks] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAtUtc] datetime2 NULL,
    [UserId] uniqueidentifier NOT NULL,
    [TaskDate] datetime2 NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [DisplayOrder] int NOT NULL,
    CONSTRAINT [PK_Tasks] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Tasks_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Tasks_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_Tasks_TenantId] ON [dbo].[Tasks] ([TenantId]);
CREATE INDEX [IX_Tasks_UserId] ON [dbo].[Tasks] ([UserId]);
CREATE INDEX [IX_Tasks_TaskDate] ON [dbo].[Tasks] ([TaskDate]);
GO
