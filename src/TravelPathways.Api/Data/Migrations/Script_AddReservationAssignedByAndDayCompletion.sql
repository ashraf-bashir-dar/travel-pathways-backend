-- =============================================
-- AssignedByUserId, ReservationDayCompletions, DayNumber on screenshots
-- Run after Script_AddReservationsTables.sql
-- =============================================

-- Reservations: add AssignedByUserId (Tour Manager who assigned = "Package confirmed by")
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Reservations') AND name = N'AssignedByUserId')
BEGIN
    ALTER TABLE dbo.Reservations
        ADD AssignedByUserId uniqueidentifier NULL;
    ALTER TABLE dbo.Reservations
        ADD CONSTRAINT FK_Reservations_Users_AssignedByUserId
        FOREIGN KEY (AssignedByUserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION;
    CREATE NONCLUSTERED INDEX IX_Reservations_AssignedByUserId ON dbo.Reservations (AssignedByUserId);
END

-- FinalNotes: final note by reservation manager
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Reservations') AND name = N'FinalNotes')
BEGIN
    ALTER TABLE dbo.Reservations
        ADD FinalNotes nvarchar(max) NULL;
END
GO

-- ReservationDayCompletions: per-day "mark as done" by reservation person
IF OBJECT_ID(N'dbo.ReservationDayCompletions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReservationDayCompletions (
        Id            uniqueidentifier NOT NULL,
        CreatedAt     datetime2(7)     NOT NULL,
        UpdatedAt     datetime2(7)     NOT NULL,
        IsDeleted     bit              NOT NULL,
        DeletedAtUtc  datetime2(7)     NULL,
        ReservationId uniqueidentifier NOT NULL,
        DayNumber     int              NOT NULL,
        IsDone        bit              NOT NULL,
        DoneAt        datetime2(7)     NULL,
        CONSTRAINT PK_ReservationDayCompletions PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ReservationDayCompletions_Reservations_ReservationId
            FOREIGN KEY (ReservationId) REFERENCES dbo.Reservations (Id) ON DELETE CASCADE
    );
    CREATE NONCLUSTERED INDEX IX_ReservationDayCompletions_ReservationId
        ON dbo.ReservationDayCompletions (ReservationId);
END
GO

-- ReservationPaymentScreenshots: optional DayNumber for hotel/day-wise screenshots
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.ReservationPaymentScreenshots') AND name = N'DayNumber')
BEGIN
    ALTER TABLE dbo.ReservationPaymentScreenshots
        ADD DayNumber int NULL;
END
GO
