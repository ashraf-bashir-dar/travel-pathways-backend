IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE TABLE [Hotels] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Address] nvarchar(max) NOT NULL,
        [City] nvarchar(max) NOT NULL,
        [State] nvarchar(max) NOT NULL,
        [Pincode] nvarchar(max) NOT NULL,
        [PhoneNumber] nvarchar(max) NOT NULL,
        [Email] nvarchar(max) NULL,
        [StarRating] int NULL,
        [IsHouseboat] bit NOT NULL,
        [Amenities] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [CheckInTime] nvarchar(max) NULL,
        [CheckOutTime] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Hotels] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE TABLE [Tenants] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Code] nvarchar(450) NOT NULL,
        [Email] nvarchar(max) NOT NULL,
        [Phone] nvarchar(max) NOT NULL,
        [Address] nvarchar(max) NOT NULL,
        [ContactPerson] nvarchar(max) NULL,
        [LogoUrl] nvarchar(max) NULL,
        [EnabledModules] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Tenants] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE TABLE [TransportCompanies] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [ContactPerson] nvarchar(max) NOT NULL,
        [PhoneNumber] nvarchar(max) NOT NULL,
        [Email] nvarchar(max) NULL,
        [Address] nvarchar(max) NOT NULL,
        [GstNumber] nvarchar(max) NULL,
        [PanNumber] nvarchar(max) NULL,
        [AadharDocumentUrl] nvarchar(max) NULL,
        [LicenceDocumentUrl] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_TransportCompanies] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE TABLE [AccommodationRates] (
        [Id] uniqueidentifier NOT NULL,
        [HotelId] uniqueidentifier NOT NULL,
        [FromDate] datetime2 NOT NULL,
        [ToDate] datetime2 NOT NULL,
        [MealPlan] nvarchar(max) NOT NULL,
        [CostPrice] decimal(18,2) NOT NULL,
        [SellingPrice] decimal(18,2) NOT NULL,
        [ExtraBedCostPrice] decimal(18,2) NULL,
        [ExtraBedSellingPrice] decimal(18,2) NULL,
        [CnbCostPrice] decimal(18,2) NULL,
        [CnbSellingPrice] decimal(18,2) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_AccommodationRates] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_AccommodationRates_Hotels_HotelId] FOREIGN KEY ([HotelId]) REFERENCES [Hotels] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE TABLE [TenantDocuments] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Type] nvarchar(max) NOT NULL,
        [FileName] nvarchar(max) NOT NULL,
        [Url] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_TenantDocuments] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_TenantDocuments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE TABLE [Users] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NULL,
        [Email] nvarchar(450) NOT NULL,
        [FirstName] nvarchar(max) NOT NULL,
        [LastName] nvarchar(max) NOT NULL,
        [Role] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [PasswordHash] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Users] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Users_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [Tenants] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE TABLE [Vehicles] (
        [Id] uniqueidentifier NOT NULL,
        [TransportCompanyId] uniqueidentifier NOT NULL,
        [VehicleType] nvarchar(max) NOT NULL,
        [VehicleModel] nvarchar(max) NULL,
        [VehicleNumber] nvarchar(max) NULL,
        [SeatingCapacity] int NOT NULL,
        [Features] nvarchar(max) NOT NULL,
        [IsAcAvailable] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Vehicles] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Vehicles_TransportCompanies_TransportCompanyId] FOREIGN KEY ([TransportCompanyId]) REFERENCES [TransportCompanies] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE TABLE [Leads] (
        [Id] uniqueidentifier NOT NULL,
        [ClientName] nvarchar(max) NOT NULL,
        [PhoneNumber] nvarchar(max) NOT NULL,
        [ClientEmail] nvarchar(max) NULL,
        [ClientState] nvarchar(max) NULL,
        [ClientCity] nvarchar(max) NULL,
        [Address] nvarchar(max) NOT NULL,
        [LeadSource] nvarchar(max) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [Notes] nvarchar(max) NULL,
        [AssignedToUserId] uniqueidentifier NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Leads] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Leads_Users_AssignedToUserId] FOREIGN KEY ([AssignedToUserId]) REFERENCES [Users] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE TABLE [VehiclePricing] (
        [Id] uniqueidentifier NOT NULL,
        [VehicleId] uniqueidentifier NOT NULL,
        [PickupLocation] nvarchar(max) NOT NULL,
        [DropLocation] nvarchar(max) NOT NULL,
        [CostPrice] decimal(18,2) NOT NULL,
        [SellingPrice] decimal(18,2) NOT NULL,
        [FromDate] datetime2 NOT NULL,
        [ToDate] datetime2 NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_VehiclePricing] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_VehiclePricing_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE TABLE [Packages] (
        [Id] uniqueidentifier NOT NULL,
        [LeadId] uniqueidentifier NULL,
        [ClientName] nvarchar(max) NOT NULL,
        [ClientPhone] nvarchar(max) NOT NULL,
        [ClientEmail] nvarchar(max) NULL,
        [ClientCity] nvarchar(max) NULL,
        [ClientState] nvarchar(max) NULL,
        [ClientPickupLocation] nvarchar(max) NOT NULL,
        [ClientDropLocation] nvarchar(max) NOT NULL,
        [PackageName] nvarchar(max) NOT NULL,
        [StartDate] datetime2 NOT NULL,
        [EndDate] datetime2 NOT NULL,
        [NumberOfDays] int NOT NULL,
        [NumberOfAdults] int NOT NULL,
        [NumberOfChildren] int NOT NULL,
        [VehicleId] uniqueidentifier NULL,
        [TotalAmount] decimal(18,2) NOT NULL,
        [AdvanceAmount] decimal(18,2) NOT NULL,
        [BalanceAmount] decimal(18,2) NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [CreatedBy] nvarchar(max) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_Packages] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Packages_Leads_LeadId] FOREIGN KEY ([LeadId]) REFERENCES [Leads] ([Id]),
        CONSTRAINT [FK_Packages_Vehicles_VehicleId] FOREIGN KEY ([VehicleId]) REFERENCES [Vehicles] ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE TABLE [DayItineraries] (
        [Id] uniqueidentifier NOT NULL,
        [PackageId] uniqueidentifier NOT NULL,
        [DayNumber] int NOT NULL,
        [Date] datetime2 NOT NULL,
        [HotelId] uniqueidentifier NULL,
        [RoomType] nvarchar(max) NULL,
        [NumberOfRooms] int NOT NULL,
        [CheckInTime] nvarchar(max) NULL,
        [CheckOutTime] nvarchar(max) NULL,
        [MealPlan] nvarchar(max) NOT NULL,
        [ExtraBedCount] int NULL,
        [CnbCount] int NULL,
        [Activities] nvarchar(max) NOT NULL,
        [Meals] nvarchar(max) NOT NULL,
        [Notes] nvarchar(max) NULL,
        [HotelCost] decimal(18,2) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [IsActive] bit NOT NULL,
        CONSTRAINT [PK_DayItineraries] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_DayItineraries_Hotels_HotelId] FOREIGN KEY ([HotelId]) REFERENCES [Hotels] ([Id]),
        CONSTRAINT [FK_DayItineraries_Packages_PackageId] FOREIGN KEY ([PackageId]) REFERENCES [Packages] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_AccommodationRates_HotelId] ON [AccommodationRates] ([HotelId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DayItineraries_HotelId] ON [DayItineraries] ([HotelId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_DayItineraries_PackageId] ON [DayItineraries] ([PackageId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Leads_AssignedToUserId] ON [Leads] ([AssignedToUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Packages_LeadId] ON [Packages] ([LeadId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Packages_VehicleId] ON [Packages] ([VehicleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_TenantDocuments_TenantId] ON [TenantDocuments] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Tenants_Code] ON [Tenants] ([Code]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Users_Email] ON [Users] ([Email]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Users_TenantId] ON [Users] ([TenantId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_VehiclePricing_VehicleId] ON [VehiclePricing] ([VehicleId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    CREATE INDEX [IX_Vehicles_TransportCompanyId] ON [Vehicles] ([TransportCompanyId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260127145954_InitialCreate'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260127145954_InitialCreate', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260131162408_AddStatesAndCities'
)
BEGIN
    CREATE TABLE [States] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [Code] nvarchar(max) NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_States] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260131162408_AddStatesAndCities'
)
BEGIN
    CREATE TABLE [Cities] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [StateId] uniqueidentifier NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Cities] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_Cities_States_StateId] FOREIGN KEY ([StateId]) REFERENCES [States] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260131162408_AddStatesAndCities'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Cities_StateId_Name] ON [Cities] ([StateId], [Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260131162408_AddStatesAndCities'
)
BEGIN
    CREATE UNIQUE INDEX [IX_States_Name] ON [States] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260131162408_AddStatesAndCities'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260131162408_AddStatesAndCities', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260131170708_AddAreasTable'
)
BEGIN
    ALTER TABLE [Hotels] ADD [AreaId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260131170708_AddAreasTable'
)
BEGIN
    ALTER TABLE [Hotels] ADD [ImageUrls] nvarchar(max) NOT NULL DEFAULT N'';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260131170708_AddAreasTable'
)
BEGIN
    CREATE TABLE [Areas] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(450) NOT NULL,
        [DisplayOrder] int NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Areas] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260131170708_AddAreasTable'
)
BEGIN
    CREATE INDEX [IX_Hotels_AreaId] ON [Hotels] ([AreaId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260131170708_AddAreasTable'
)
BEGIN
    CREATE UNIQUE INDEX [IX_Areas_Name] ON [Areas] ([Name]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260131170708_AddAreasTable'
)
BEGIN
    ALTER TABLE [Hotels] ADD CONSTRAINT [FK_Hotels_Areas_AreaId] FOREIGN KEY ([AreaId]) REFERENCES [Areas] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260131170708_AddAreasTable'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260131170708_AddAreasTable', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203074025_AddPlansAndSubscription'
)
BEGIN
    ALTER TABLE [Users] ADD [AllowedModules] nvarchar(max) NOT NULL DEFAULT N'[]';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203074025_AddPlansAndSubscription'
)
BEGIN
    ALTER TABLE [Tenants] ADD [BillingCycle] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203074025_AddPlansAndSubscription'
)
BEGIN
    ALTER TABLE [Tenants] ADD [DefaultUserId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203074025_AddPlansAndSubscription'
)
BEGIN
    ALTER TABLE [Tenants] ADD [PlanId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203074025_AddPlansAndSubscription'
)
BEGIN
    ALTER TABLE [Tenants] ADD [SeatsPurchased] int NOT NULL DEFAULT 0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203074025_AddPlansAndSubscription'
)
BEGIN
    ALTER TABLE [Tenants] ADD [SubscriptionEndUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203074025_AddPlansAndSubscription'
)
BEGIN
    ALTER TABLE [Tenants] ADD [SubscriptionStartUtc] datetime2 NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203074025_AddPlansAndSubscription'
)
BEGIN
    ALTER TABLE [Tenants] ADD [SubscriptionStatus] nvarchar(max) NOT NULL DEFAULT N'Active';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203074025_AddPlansAndSubscription'
)
BEGIN
    CREATE TABLE [Plans] (
        [Id] uniqueidentifier NOT NULL,
        [Name] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_Plans] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203074025_AddPlansAndSubscription'
)
BEGIN
    CREATE TABLE [PlanPrices] (
        [Id] uniqueidentifier NOT NULL,
        [PlanId] uniqueidentifier NOT NULL,
        [BillingCycle] nvarchar(450) NOT NULL,
        [BasePriceInr] decimal(18,2) NOT NULL,
        [PricePerUserInr] decimal(18,2) NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_PlanPrices] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_PlanPrices_Plans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [Plans] ([Id]) ON DELETE CASCADE
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203074025_AddPlansAndSubscription'
)
BEGIN
    CREATE INDEX [IX_Tenants_DefaultUserId] ON [Tenants] ([DefaultUserId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203074025_AddPlansAndSubscription'
)
BEGIN
    CREATE INDEX [IX_Tenants_PlanId] ON [Tenants] ([PlanId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203074025_AddPlansAndSubscription'
)
BEGIN
    CREATE UNIQUE INDEX [IX_PlanPrices_PlanId_BillingCycle] ON [PlanPrices] ([PlanId], [BillingCycle]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203074025_AddPlansAndSubscription'
)
BEGIN
    ALTER TABLE [Tenants] ADD CONSTRAINT [FK_Tenants_Plans_PlanId] FOREIGN KEY ([PlanId]) REFERENCES [Plans] ([Id]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203074025_AddPlansAndSubscription'
)
BEGIN
    ALTER TABLE [Tenants] ADD CONSTRAINT [FK_Tenants_Users_DefaultUserId] FOREIGN KEY ([DefaultUserId]) REFERENCES [Users] ([Id]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260203074025_AddPlansAndSubscription'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260203074025_AddPlansAndSubscription', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207101525_AddPackageDiscount'
)
BEGIN
    ALTER TABLE [Packages] ADD [Discount] decimal(18,2) NOT NULL DEFAULT 0.0;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207101525_AddPackageDiscount'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260207101525_AddPackageDiscount', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207120000_AddItineraryTemplates'
)
BEGIN
    CREATE TABLE [ItineraryTemplates] (
        [Id] uniqueidentifier NOT NULL,
        [TenantId] uniqueidentifier NOT NULL,
        [Title] nvarchar(max) NOT NULL,
        [Description] nvarchar(max) NOT NULL,
        [IsActive] bit NOT NULL,
        [CreatedAt] datetime2 NOT NULL,
        [UpdatedAt] datetime2 NOT NULL,
        CONSTRAINT [PK_ItineraryTemplates] PRIMARY KEY ([Id])
    );
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207120000_AddItineraryTemplates'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260207120000_AddItineraryTemplates', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207130000_RenameItineraryTemplatesToDestinationMaster'
)
BEGIN
    EXEC sp_rename N'[ItineraryTemplates]', N'DestinationMaster';
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260207130000_RenameItineraryTemplatesToDestinationMaster'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260207130000_RenameItineraryTemplatesToDestinationMaster', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209120000_AddDayItineraryTemplateId'
)
BEGIN
    ALTER TABLE [DayItineraries] ADD [ItineraryTemplateId] uniqueidentifier NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209120000_AddDayItineraryTemplateId'
)
BEGIN
    CREATE INDEX [IX_DayItineraries_ItineraryTemplateId] ON [DayItineraries] ([ItineraryTemplateId]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209120000_AddDayItineraryTemplateId'
)
BEGIN
    ALTER TABLE [DayItineraries] ADD CONSTRAINT [FK_DayItineraries_DestinationMaster_ItineraryTemplateId] FOREIGN KEY ([ItineraryTemplateId]) REFERENCES [DestinationMaster] ([Id]) ON DELETE SET NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260209120000_AddDayItineraryTemplateId'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260209120000_AddDayItineraryTemplateId', N'8.0.22');
END;
GO

COMMIT;
GO

BEGIN TRANSACTION;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260217062720_AddUserDepartment'
)
BEGIN
    ALTER TABLE [DayItineraries] DROP CONSTRAINT [FK_DayItineraries_DestinationMaster_ItineraryTemplateId];
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260217062720_AddUserDepartment'
)
BEGIN
    ALTER TABLE [Users] ADD [Department] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260217062720_AddUserDepartment'
)
BEGIN
    DECLARE @var0 sysname;
    SELECT @var0 = [d].[name]
    FROM [sys].[default_constraints] [d]
    INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
    WHERE ([d].[parent_object_id] = OBJECT_ID(N'[AccommodationRates]') AND [c].[name] = N'RoomCategory');
    IF @var0 IS NOT NULL EXEC(N'ALTER TABLE [AccommodationRates] DROP CONSTRAINT [' + @var0 + '];');
    ALTER TABLE [AccommodationRates] ALTER COLUMN [RoomCategory] nvarchar(max) NULL;
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260217062720_AddUserDepartment'
)
BEGIN
    ALTER TABLE [DayItineraries] ADD CONSTRAINT [FK_DayItineraries_DestinationMaster_ItineraryTemplateId] FOREIGN KEY ([ItineraryTemplateId]) REFERENCES [DestinationMaster] ([Id]);
END;
GO

IF NOT EXISTS (
    SELECT * FROM [__EFMigrationsHistory]
    WHERE [MigrationId] = N'20260217062720_AddUserDepartment'
)
BEGIN
    INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
    VALUES (N'20260217062720_AddUserDepartment', N'8.0.22');
END;
GO

COMMIT;
GO

