-- Create Attendance table (one row per user per day: Time In / Time Out).
-- Safe to run: skips if table already exists.

IF OBJECT_ID(N'dbo.Attendance', N'U') IS NOT NULL
  RETURN;

CREATE TABLE [dbo].[Attendance] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAtUtc] datetime2 NULL,
    [UserId] uniqueidentifier NOT NULL,
    [AttendanceDate] date NOT NULL,
    [TimeInUtc] datetime2 NULL,
    [TimeOutUtc] datetime2 NULL,
    CONSTRAINT [PK_Attendance] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Attendance_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Attendance_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [UQ_Attendance_TenantUserDate] UNIQUE ([TenantId], [UserId], [AttendanceDate])
);

CREATE INDEX [IX_Attendance_TenantId] ON [dbo].[Attendance] ([TenantId]);
CREATE INDEX [IX_Attendance_UserId] ON [dbo].[Attendance] ([UserId]);
CREATE INDEX [IX_Attendance_AttendanceDate] ON [dbo].[Attendance] ([AttendanceDate]);
GO
