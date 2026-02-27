-- Create Payments table (received from clients, made to hotel/houseboat/transport/employee/driver/office).
-- Run this if the table does not exist. Safe to run: skips if table already exists.
-- If Payments already exists but is missing PayeeCategory/UserId/PayeeDescription, run AddPaymentPayeeColumns.sql instead.

IF OBJECT_ID(N'dbo.Payments', N'U') IS NOT NULL
  RETURN;

CREATE TABLE [dbo].[Payments] (
    [Id] uniqueidentifier NOT NULL,
    [TenantId] uniqueidentifier NOT NULL,
    [IsActive] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL,
    [UpdatedAt] datetime2 NOT NULL,
    [IsDeleted] bit NOT NULL,
    [DeletedAtUtc] datetime2 NULL,
    [PaymentType] nvarchar(max) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [PaymentDate] datetime2 NOT NULL,
    [Reference] nvarchar(max) NULL,
    [Notes] nvarchar(max) NULL,
    [LeadId] uniqueidentifier NULL,
    [PackageId] uniqueidentifier NULL,
    [HotelId] uniqueidentifier NULL,
    [TransportCompanyId] uniqueidentifier NULL,
    [PayeeCategory] nvarchar(max) NULL,
    [UserId] uniqueidentifier NULL,
    [PayeeDescription] nvarchar(max) NULL,
    [ScreenshotUrl] nvarchar(max) NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_Payments_Tenants_TenantId] FOREIGN KEY ([TenantId]) REFERENCES [dbo].[Tenants] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_Payments_Leads_LeadId] FOREIGN KEY ([LeadId]) REFERENCES [dbo].[Leads] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Payments_Packages_PackageId] FOREIGN KEY ([PackageId]) REFERENCES [dbo].[Packages] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Payments_Hotels_HotelId] FOREIGN KEY ([HotelId]) REFERENCES [dbo].[Hotels] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Payments_TransportCompanies_TransportCompanyId] FOREIGN KEY ([TransportCompanyId]) REFERENCES [dbo].[TransportCompanies] ([Id]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Payments_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION
);

CREATE INDEX [IX_Payments_TenantId] ON [dbo].[Payments] ([TenantId]);
CREATE INDEX [IX_Payments_LeadId] ON [dbo].[Payments] ([LeadId]);
CREATE INDEX [IX_Payments_PackageId] ON [dbo].[Payments] ([PackageId]);
CREATE INDEX [IX_Payments_HotelId] ON [dbo].[Payments] ([HotelId]);
CREATE INDEX [IX_Payments_TransportCompanyId] ON [dbo].[Payments] ([TransportCompanyId]);
CREATE INDEX [IX_Payments_UserId] ON [dbo].[Payments] ([UserId]);
CREATE INDEX [IX_Payments_PaymentDate] ON [dbo].[Payments] ([PaymentDate]);
GO
