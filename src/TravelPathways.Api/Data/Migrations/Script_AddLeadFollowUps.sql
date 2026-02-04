-- Create LeadFollowUps table (run this if dotnet ef database update is not used)
-- Use the same database as your app (ConnectionStrings:DefaultConnection)

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'LeadFollowUps')
BEGIN
    CREATE TABLE [LeadFollowUps] (
        [Id] uniqueidentifier NOT NULL,
        [LeadId] uniqueidentifier NOT NULL,
        [FollowUpDate] datetime2 NOT NULL,
        [Status] nvarchar(max) NOT NULL,
        [Notes] nvarchar(max) NULL,
        [CreatedAt] datetime2 NOT NULL,
        [CreatedBy] nvarchar(max) NULL,
        CONSTRAINT [PK_LeadFollowUps] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_LeadFollowUps_Leads_LeadId] FOREIGN KEY ([LeadId]) REFERENCES [Leads] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_LeadFollowUps_LeadId] ON [LeadFollowUps] ([LeadId]);
END
GO
