-- Rename existing EmployeeCompensations table to EmployeeSalary.
-- Run this only if you already created EmployeeCompensations and want to use the new name.

IF OBJECT_ID(N'dbo.EmployeeCompensations', N'U') IS NULL
  RETURN;

IF OBJECT_ID(N'dbo.EmployeeSalary', N'U') IS NOT NULL
  RETURN;

EXEC sp_rename 'dbo.EmployeeCompensations', 'EmployeeSalary';
EXEC sp_rename 'dbo.EmployeeSalary.PK_EmployeeCompensations', 'PK_EmployeeSalary', 'OBJECT';
EXEC sp_rename 'dbo.EmployeeSalary.FK_EmployeeCompensations_Tenants_TenantId', 'FK_EmployeeSalary_Tenants_TenantId', 'OBJECT';
EXEC sp_rename 'dbo.EmployeeSalary.FK_EmployeeCompensations_Users_UserId', 'FK_EmployeeSalary_Users_UserId', 'OBJECT';
EXEC sp_rename 'dbo.EmployeeSalary.IX_EmployeeCompensations_TenantId', 'IX_EmployeeSalary_TenantId', 'INDEX';
EXEC sp_rename 'dbo.EmployeeSalary.IX_EmployeeCompensations_UserId', 'IX_EmployeeSalary_UserId', 'INDEX';
GO
