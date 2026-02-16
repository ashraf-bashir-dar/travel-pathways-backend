-- Rename table ItineraryTemplates to DestinationMaster
-- Run this if you already have ItineraryTemplates and are not using EF migrations.

IF EXISTS (SELECT * FROM sys.tables WHERE name = 'ItineraryTemplates')
BEGIN
  EXEC sp_rename 'ItineraryTemplates', 'DestinationMaster';
END
GO
