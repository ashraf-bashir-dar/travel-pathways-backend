-- Add StartTime and EndTime to Tasks (daily tasks); max 2 hours per task.
-- Safe to run: skips if columns already exist.

IF NOT EXISTS (
  SELECT 1 FROM sys.columns c
  INNER JOIN sys.tables t ON c.object_id = t.object_id
  WHERE t.name = 'Tasks' AND c.name = 'StartTimeUtc'
)
BEGIN
  ALTER TABLE [dbo].[Tasks] ADD [StartTimeUtc] datetime2 NULL;
END
GO

IF NOT EXISTS (
  SELECT 1 FROM sys.columns c
  INNER JOIN sys.tables t ON c.object_id = t.object_id
  WHERE t.name = 'Tasks' AND c.name = 'EndTimeUtc'
)
BEGIN
  ALTER TABLE [dbo].[Tasks] ADD [EndTimeUtc] datetime2 NULL;
END
GO
