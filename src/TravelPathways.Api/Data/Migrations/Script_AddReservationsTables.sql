-- =============================================
-- Reservations module tables
-- Equivalent to migration 20260225180000_AddReservationsModule
-- Requires: Packages, Tenants, Users tables must exist
-- =============================================

-- Reservations: assignment of confirmed packages to reservation managers
IF OBJECT_ID(N'dbo.Reservations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Reservations (
        Id                 uniqueidentifier NOT NULL,
        TenantId           uniqueidentifier NOT NULL,
        IsActive           bit              NOT NULL,
        CreatedAt          datetime2(7)     NOT NULL,
        UpdatedAt          datetime2(7)     NOT NULL,
        IsDeleted          bit              NOT NULL,
        DeletedAtUtc       datetime2(7)     NULL,
        PackageId          uniqueidentifier NOT NULL,
        AssignedToUserId   uniqueidentifier NOT NULL,
        Status             nvarchar(max)   NOT NULL,
        Notes              nvarchar(max)   NULL,
        CONSTRAINT PK_Reservations PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Reservations_Packages_PackageId
            FOREIGN KEY (PackageId) REFERENCES dbo.Packages (Id) ON DELETE NO ACTION,
        CONSTRAINT FK_Reservations_Tenants_TenantId
            FOREIGN KEY (TenantId) REFERENCES dbo.Tenants (Id) ON DELETE CASCADE,
        CONSTRAINT FK_Reservations_Users_AssignedToUserId
            FOREIGN KEY (AssignedToUserId) REFERENCES dbo.Users (Id) ON DELETE NO ACTION
    );

    CREATE NONCLUSTERED INDEX IX_Reservations_PackageId
        ON dbo.Reservations (PackageId);
    CREATE NONCLUSTERED INDEX IX_Reservations_AssignedToUserId
        ON dbo.Reservations (AssignedToUserId);
    CREATE NONCLUSTERED INDEX IX_Reservations_TenantId
        ON dbo.Reservations (TenantId);
END
GO

-- ReservationPaymentScreenshots: advance payment receipts per reservation
IF OBJECT_ID(N'dbo.ReservationPaymentScreenshots', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ReservationPaymentScreenshots (
        Id             uniqueidentifier NOT NULL,
        CreatedAt      datetime2(7)     NOT NULL,
        UpdatedAt      datetime2(7)     NOT NULL,
        IsDeleted      bit              NOT NULL,
        DeletedAtUtc   datetime2(7)     NULL,
        ReservationId  uniqueidentifier NOT NULL,
        FileUrl        nvarchar(max)    NOT NULL,
        FileName       nvarchar(max)    NOT NULL,
        CONSTRAINT PK_ReservationPaymentScreenshots PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_ReservationPaymentScreenshots_Reservations_ReservationId
            FOREIGN KEY (ReservationId) REFERENCES dbo.Reservations (Id) ON DELETE CASCADE
    );

    CREATE NONCLUSTERED INDEX IX_ReservationPaymentScreenshots_ReservationId
        ON dbo.ReservationPaymentScreenshots (ReservationId);
END
GO
