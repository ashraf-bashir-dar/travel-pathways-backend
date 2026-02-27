-- Rename existing EmployeeDailyTasks table to Tasks (constraints and indexes are renamed with the table in SQL Server).
-- Run this only if you already have EmployeeDailyTasks and want to switch to the new name. Safe to run: skips if Tasks already exists or EmployeeDailyTasks does not exist.

IF OBJECT_ID(N'dbo.Tasks', N'U') IS NOT NULL
  RETURN;

IF OBJECT_ID(N'dbo.EmployeeDailyTasks', N'U') IS NULL
  RETURN;

EXEC sp_rename N'dbo.EmployeeDailyTasks', N'Tasks', N'OBJECT';

-- Rename constraints and indexes (sp_rename renames the table but keeps old constraint/index names)
DECLARE @sql nvarchar(max);

IF EXISTS (SELECT 1 FROM sys.key_constraints WHERE name = 'PK_EmployeeDailyTasks' AND parent_object_id = OBJECT_ID('dbo.Tasks'))
BEGIN
  EXEC sp_rename N'dbo.PK_EmployeeDailyTasks', N'PK_Tasks', N'OBJECT';
END

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeDailyTasks_Tenants_TenantId')
BEGIN
  EXEC sp_rename N'dbo.FK_EmployeeDailyTasks_Tenants_TenantId', N'FK_Tasks_Tenants_TenantId', N'OBJECT';
END

IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_EmployeeDailyTasks_Users_UserId')
BEGIN
  EXEC sp_rename N'dbo.FK_EmployeeDailyTasks_Users_UserId', N'FK_Tasks_Users_UserId', N'OBJECT';
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeDailyTasks_TenantId' AND object_id = OBJECT_ID('dbo.Tasks'))
BEGIN
  EXEC sp_rename N'dbo.Tasks.IX_EmployeeDailyTasks_TenantId', N'IX_Tasks_TenantId', N'INDEX';
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeDailyTasks_UserId' AND object_id = OBJECT_ID('dbo.Tasks'))
BEGIN
  EXEC sp_rename N'dbo.Tasks.IX_EmployeeDailyTasks_UserId', N'IX_Tasks_UserId', N'INDEX';
END

IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EmployeeDailyTasks_TaskDate' AND object_id = OBJECT_ID('dbo.Tasks'))
BEGIN
  EXEC sp_rename N'dbo.Tasks.IX_EmployeeDailyTasks_TaskDate', N'IX_Tasks_TaskDate', N'INDEX';
END

GO
