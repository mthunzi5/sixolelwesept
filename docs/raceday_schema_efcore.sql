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
CREATE TABLE [Users] (
    [UserID] int NOT NULL IDENTITY,
    [FullName] nvarchar(100) NOT NULL,
    [Email] nvarchar(150) NOT NULL,
    [PasswordHash] nvarchar(255) NOT NULL,
    [Role] varchar(20) NOT NULL,
    [PhoneNumber] nvarchar(20) NULL,
    [CreatedAt] datetime NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_Users] PRIMARY KEY ([UserID]),
    CONSTRAINT [CK_Users_Role] CHECK ([Role] IN ('Organiser', 'Participant'))
);

CREATE TABLE [Events] (
    [EventID] int NOT NULL IDENTITY,
    [OrganiserID] int NOT NULL,
    [EventName] nvarchar(150) NOT NULL,
    [EventDate] date NOT NULL,
    [Location] nvarchar(150) NOT NULL,
    [Description] nvarchar(500) NULL,
    [DistanceKm] decimal(5,2) NOT NULL,
    [EventType] varchar(10) NOT NULL,
    [Status] varchar(20) NOT NULL,
    [CreatedAt] datetime NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_Events] PRIMARY KEY ([EventID]),
    CONSTRAINT [CK_Events_Distance] CHECK ([DistanceKm] > 0),
    CONSTRAINT [CK_Events_EventType] CHECK ([EventType] IN ('Run', 'Walk', 'Cycle')),
    CONSTRAINT [CK_Events_Status] CHECK ([Status] IN ('Scheduled', 'Ongoing', 'Completed', 'Cancelled')),
    CONSTRAINT [FK_Events_Organiser] FOREIGN KEY ([OrganiserID]) REFERENCES [Users] ([UserID]) ON DELETE NO ACTION
);

CREATE TABLE [Categories] (
    [CategoryID] int NOT NULL IDENTITY,
    [EventID] int NOT NULL,
    [CategoryName] varchar(50) NOT NULL,
    [CategoryType] varchar(10) NOT NULL,
    [DistanceKm] decimal(5,2) NOT NULL,
    [MinAge] int NULL,
    [MaxAge] int NULL,
    [EntryFee] decimal(8,2) NOT NULL,
    [MaxParticipants] int NOT NULL,
    CONSTRAINT [PK_Categories] PRIMARY KEY ([CategoryID]),
    CONSTRAINT [CK_Categories_CategoryType] CHECK ([CategoryType] IN ('Distance', 'Age')),
    CONSTRAINT [CK_Categories_Distance] CHECK ([DistanceKm] > 0),
    CONSTRAINT [CK_Categories_MaxParticipants] CHECK ([MaxParticipants] > 0),
    CONSTRAINT [FK_Categories_Event] FOREIGN KEY ([EventID]) REFERENCES [Events] ([EventID]) ON DELETE NO ACTION
);

CREATE TABLE [Enrolments] (
    [EnrolmentID] int NOT NULL IDENTITY,
    [ParticipantID] int NOT NULL,
    [CategoryID] int NOT NULL,
    [BibNumber] varchar(10) NOT NULL,
    [EnrolmentDate] datetime NOT NULL DEFAULT (GETDATE()),
    [Status] varchar(20) NOT NULL,
    CONSTRAINT [PK_Enrolments] PRIMARY KEY ([EnrolmentID]),
    CONSTRAINT [CK_Enrolments_Status] CHECK ([Status] IN ('Pending', 'Confirmed', 'Withdrawn')),
    CONSTRAINT [FK_Enrolments_Category] FOREIGN KEY ([CategoryID]) REFERENCES [Categories] ([CategoryID]) ON DELETE NO ACTION,
    CONSTRAINT [FK_Enrolments_Participant] FOREIGN KEY ([ParticipantID]) REFERENCES [Users] ([UserID]) ON DELETE NO ACTION
);

CREATE TABLE [Payments] (
    [PaymentID] int NOT NULL IDENTITY,
    [EnrolmentID] int NOT NULL,
    [Amount] decimal(8,2) NOT NULL,
    [PaymentMethod] varchar(30) NOT NULL,
    [PaymentDate] datetime NOT NULL DEFAULT (GETDATE()),
    [Status] varchar(20) NOT NULL,
    CONSTRAINT [PK_Payments] PRIMARY KEY ([PaymentID]),
    CONSTRAINT [CK_Payments_Method] CHECK ([PaymentMethod] IN ('Card', 'EFT', 'Cash')),
    CONSTRAINT [CK_Payments_Status] CHECK ([Status] IN ('Pending', 'Paid', 'Refunded')),
    CONSTRAINT [FK_Payments_Enrolment] FOREIGN KEY ([EnrolmentID]) REFERENCES [Enrolments] ([EnrolmentID]) ON DELETE NO ACTION
);

CREATE TABLE [Results] (
    [ResultID] int NOT NULL IDENTITY,
    [EnrolmentID] int NOT NULL,
    [FinishTimeSeconds] int NULL,
    [Position] int NULL,
    [Status] varchar(20) NOT NULL,
    CONSTRAINT [PK_Results] PRIMARY KEY ([ResultID]),
    CONSTRAINT [CK_Results_Status] CHECK ([Status] IN ('Finished', 'DNF', 'DSQ')),
    CONSTRAINT [FK_Results_Enrolment] FOREIGN KEY ([EnrolmentID]) REFERENCES [Enrolments] ([EnrolmentID]) ON DELETE NO ACTION
);

CREATE UNIQUE INDEX [UQ_Categories_EventName] ON [Categories] ([EventID], [CategoryName]);

CREATE INDEX [IX_Enrolments_CategoryID] ON [Enrolments] ([CategoryID]);

CREATE UNIQUE INDEX [UQ_Enrolments_Bib] ON [Enrolments] ([BibNumber]);

CREATE UNIQUE INDEX [UQ_Enrolments_Participant_Category] ON [Enrolments] ([ParticipantID], [CategoryID]);

CREATE INDEX [IX_Events_OrganiserID] ON [Events] ([OrganiserID]);

CREATE UNIQUE INDEX [UQ_Payments_Enrolment] ON [Payments] ([EnrolmentID]);

CREATE UNIQUE INDEX [UQ_Results_Enrolment] ON [Results] ([EnrolmentID]);

CREATE UNIQUE INDEX [UQ_Users_Email] ON [Users] ([Email]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261019212751_InitialCreate', N'9.0.10');

COMMIT;
GO

