-- Add EmployeePaymentKind to Payments (Salary, Incentive, Bonus for employee payments).
-- Run once. Requires: Payments table must already exist.

IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
BEGIN
  RAISERROR(N'Payments table does not exist. Run CreatePaymentsTable.sql first.', 16, 1);
  RETURN;
END
GO

IF COL_LENGTH('dbo.Payments', N'EmployeePaymentKind') IS NULL
BEGIN
  ALTER TABLE [dbo].[Payments] ADD [EmployeePaymentKind] nvarchar(max) NULL;
END
GO
