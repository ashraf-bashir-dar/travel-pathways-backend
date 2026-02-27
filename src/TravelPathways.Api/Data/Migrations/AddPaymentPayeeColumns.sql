-- Add PayeeCategory, UserId, PayeeDescription to Payments (Option A: unified payee categories).
-- Run once. Requires: Payments table must already exist.
-- If Payments does NOT exist, run CreatePaymentsTable.sql first.

IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
BEGIN
  RAISERROR(N'Payments table does not exist. Run CreatePaymentsTable.sql first.', 16, 1);
  RETURN;
END
GO

IF COL_LENGTH('dbo.Payments', N'PayeeCategory') IS NULL
BEGIN
  ALTER TABLE [dbo].[Payments] ADD [PayeeCategory] nvarchar(max) NULL;
END
GO

IF COL_LENGTH('dbo.Payments', N'PayeeDescription') IS NULL
BEGIN
  ALTER TABLE [dbo].[Payments] ADD [PayeeDescription] nvarchar(max) NULL;
END
GO

IF COL_LENGTH('dbo.Payments', N'UserId') IS NULL
BEGIN
  ALTER TABLE [dbo].[Payments] ADD [UserId] uniqueidentifier NULL;
  ALTER TABLE [dbo].[Payments] ADD CONSTRAINT [FK_Payments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION;
  CREATE INDEX [IX_Payments_UserId] ON [dbo].[Payments] ([UserId]);
END
GO
