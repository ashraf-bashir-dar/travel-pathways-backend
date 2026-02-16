-- Package status was aligned with lead status. Run this once if you have existing packages with old status values.
-- Old: Draft, Quoted, Confirmed, InProgress, Completed, Cancelled
-- New: New, FollowUp, PlanPostponed, PlanCanceled, Confirmed

UPDATE Packages SET Status = 'New' WHERE Status = 'Draft';
UPDATE Packages SET Status = 'FollowUp' WHERE Status = 'Quoted';
UPDATE Packages SET Status = 'FollowUp' WHERE Status = 'InProgress';
UPDATE Packages SET Status = 'Confirmed' WHERE Status = 'Completed';
UPDATE Packages SET Status = 'PlanCanceled' WHERE Status = 'Cancelled';
