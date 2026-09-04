/* ==========================================================================
   RaceDay System - Database Creation Script
   Part 1, Section C

   Target: Microsoft SQL Server (tested via SSMS / sqlcmd against a clean
   instance). Run the whole file top to bottom - it creates the database,
   builds every table from the ERD in docs/erd.png, and seeds sample data.

   Entities (matches ERD exactly):
     Users -> Events -> Categories -> Enrolments -> Results
                                                   -> Payments
   ========================================================================== */

USE master;
GO

IF DB_ID('RaceDayDB') IS NOT NULL
BEGIN
    ALTER DATABASE RaceDayDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
    DROP DATABASE RaceDayDB;
END
GO

CREATE DATABASE RaceDayDB;
GO

USE RaceDayDB;
GO

/* ==========================================================================
   1. TABLE CREATION
   ========================================================================== */

CREATE TABLE Users (
    UserID        INT IDENTITY(1,1) PRIMARY KEY,
    FullName      NVARCHAR(100)  NOT NULL,
    Email         NVARCHAR(150)  NOT NULL,
    PasswordHash  NVARCHAR(255)  NOT NULL,
    Role          VARCHAR(20)    NOT NULL,
    PhoneNumber   NVARCHAR(20)   NULL,
    CreatedAt     DATETIME       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT UQ_Users_Email UNIQUE (Email),
    CONSTRAINT CK_Users_Role CHECK (Role IN ('Organiser', 'Participant'))
);
GO

CREATE TABLE Events (
    EventID       INT IDENTITY(1,1) PRIMARY KEY,
    OrganiserID   INT            NOT NULL,
    EventName     NVARCHAR(150)  NOT NULL,
    EventDate     DATE           NOT NULL,
    Location      NVARCHAR(150)  NOT NULL,
    Description   NVARCHAR(500)  NULL,
    Status        VARCHAR(20)    NOT NULL DEFAULT 'Scheduled',
    CreatedAt     DATETIME       NOT NULL DEFAULT GETDATE(),
    CONSTRAINT FK_Events_Organiser FOREIGN KEY (OrganiserID) REFERENCES Users(UserID),
    CONSTRAINT CK_Events_Status CHECK (Status IN ('Scheduled', 'Ongoing', 'Completed', 'Cancelled'))
);
GO

CREATE TABLE Categories (
    CategoryID       INT IDENTITY(1,1) PRIMARY KEY,
    EventID          INT            NOT NULL,
    CategoryName     VARCHAR(50)    NOT NULL,
    DistanceKm       DECIMAL(5,2)   NOT NULL,
    EntryFee         DECIMAL(8,2)   NOT NULL DEFAULT 0,
    MaxParticipants  INT            NOT NULL,
    CONSTRAINT FK_Categories_Event FOREIGN KEY (EventID) REFERENCES Events(EventID),
    CONSTRAINT UQ_Categories_EventName UNIQUE (EventID, CategoryName),
    CONSTRAINT CK_Categories_Distance CHECK (DistanceKm > 0),
    CONSTRAINT CK_Categories_MaxParticipants CHECK (MaxParticipants > 0)
);
GO

CREATE TABLE Enrolments (
    EnrolmentID     INT IDENTITY(1,1) PRIMARY KEY,
    ParticipantID   INT            NOT NULL,
    CategoryID      INT            NOT NULL,
    BibNumber       VARCHAR(10)    NOT NULL,
    EnrolmentDate   DATETIME       NOT NULL DEFAULT GETDATE(),
    Status          VARCHAR(20)    NOT NULL DEFAULT 'Confirmed',
    CONSTRAINT FK_Enrolments_Participant FOREIGN KEY (ParticipantID) REFERENCES Users(UserID),
    CONSTRAINT FK_Enrolments_Category FOREIGN KEY (CategoryID) REFERENCES Categories(CategoryID),
    CONSTRAINT UQ_Enrolments_Bib UNIQUE (BibNumber),
    CONSTRAINT UQ_Enrolments_Participant_Category UNIQUE (ParticipantID, CategoryID),
    CONSTRAINT CK_Enrolments_Status CHECK (Status IN ('Pending', 'Confirmed', 'Withdrawn'))
);
GO

CREATE TABLE Results (
    ResultID            INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentID         INT           NOT NULL,
    FinishTimeSeconds   INT           NULL,
    Position            INT           NULL,
    Status              VARCHAR(20)   NOT NULL DEFAULT 'Finished',
    CONSTRAINT FK_Results_Enrolment FOREIGN KEY (EnrolmentID) REFERENCES Enrolments(EnrolmentID),
    CONSTRAINT UQ_Results_Enrolment UNIQUE (EnrolmentID),
    CONSTRAINT CK_Results_Status CHECK (Status IN ('Finished', 'DNF', 'DSQ'))
);
GO

CREATE TABLE Payments (
    PaymentID       INT IDENTITY(1,1) PRIMARY KEY,
    EnrolmentID     INT            NOT NULL,
    Amount          DECIMAL(8,2)   NOT NULL,
    PaymentMethod   VARCHAR(30)    NOT NULL,
    PaymentDate     DATETIME       NOT NULL DEFAULT GETDATE(),
    Status          VARCHAR(20)    NOT NULL DEFAULT 'Pending',
    CONSTRAINT FK_Payments_Enrolment FOREIGN KEY (EnrolmentID) REFERENCES Enrolments(EnrolmentID),
    CONSTRAINT UQ_Payments_Enrolment UNIQUE (EnrolmentID),
    CONSTRAINT CK_Payments_Method CHECK (PaymentMethod IN ('Card', 'EFT', 'Cash')),
    CONSTRAINT CK_Payments_Status CHECK (Status IN ('Pending', 'Paid', 'Refunded'))
);
GO

/* ==========================================================================
   2. SEED DATA
   Natural keys (Email / EventName / CategoryName / BibNumber) are used to
   look up IDENTITY values in later inserts instead of hard-coding numbers,
   so the script stays correct even if IDENTITY seeding ever changes.
   ========================================================================== */

-- 2 Organisers + 4 Participants
INSERT INTO Users (FullName, Email, PasswordHash, Role, PhoneNumber) VALUES
    ('Naledi Khumalo',  'naledi.khumalo@raceday.co.za',  'HASH_PLACEHOLDER_1', 'Organiser',   '0821234501'),
    ('Sipho Dlamini',   'sipho.dlamini@raceday.co.za',   'HASH_PLACEHOLDER_2', 'Organiser',   '0821234502'),
    ('Amahle Nkosi',    'amahle.nkosi@example.com',      'HASH_PLACEHOLDER_3', 'Participant', '0731234601'),
    ('Jason van Wyk',   'jason.vanwyk@example.com',      'HASH_PLACEHOLDER_4', 'Participant', '0731234602'),
    ('Palesa Mokoena',  'palesa.mokoena@example.com',    'HASH_PLACEHOLDER_5', 'Participant', '0731234603'),
    ('Ryan Govender',   'ryan.govender@example.com',     'HASH_PLACEHOLDER_6', 'Participant', '0731234604');
GO

-- 3 Events, spread across the 2 organisers
INSERT INTO Events (OrganiserID, EventName, EventDate, Location, Description, Status)
SELECT UserID, 'Johannesburg City Marathon', '2026-10-18', 'Johannesburg, Gauteng',
       'Annual road race through the Johannesburg CBD with 5km, 10km and 21km categories.', 'Scheduled'
FROM Users WHERE Email = 'naledi.khumalo@raceday.co.za';

INSERT INTO Events (OrganiserID, EventName, EventDate, Location, Description, Status)
SELECT UserID, 'Durban Beachfront Fun Run', '2026-11-08', 'Durban, KwaZulu-Natal',
       'Family-friendly fun run along the Durban beachfront promenade.', 'Scheduled'
FROM Users WHERE Email = 'naledi.khumalo@raceday.co.za';

INSERT INTO Events (OrganiserID, EventName, EventDate, Location, Description, Status)
SELECT UserID, 'Cape Town Trail Challenge', '2026-09-27', 'Cape Town, Western Cape',
       'Off-road trail event on the slopes of Table Mountain.', 'Scheduled'
FROM Users WHERE Email = 'sipho.dlamini@raceday.co.za';
GO

-- Categories for each event
INSERT INTO Categories (EventID, CategoryName, DistanceKm, EntryFee, MaxParticipants)
SELECT EventID, '5km Fun Run', 5.00, 100.00, 500 FROM Events WHERE EventName = 'Johannesburg City Marathon';
INSERT INTO Categories (EventID, CategoryName, DistanceKm, EntryFee, MaxParticipants)
SELECT EventID, '10km Road Race', 10.00, 180.00, 400 FROM Events WHERE EventName = 'Johannesburg City Marathon';
INSERT INTO Categories (EventID, CategoryName, DistanceKm, EntryFee, MaxParticipants)
SELECT EventID, '21km Half Marathon', 21.10, 280.00, 300 FROM Events WHERE EventName = 'Johannesburg City Marathon';

INSERT INTO Categories (EventID, CategoryName, DistanceKm, EntryFee, MaxParticipants)
SELECT EventID, '3km Fun Run', 3.00, 60.00, 800 FROM Events WHERE EventName = 'Durban Beachfront Fun Run';
INSERT INTO Categories (EventID, CategoryName, DistanceKm, EntryFee, MaxParticipants)
SELECT EventID, '8km Beach Run', 8.00, 120.00, 350 FROM Events WHERE EventName = 'Durban Beachfront Fun Run';

INSERT INTO Categories (EventID, CategoryName, DistanceKm, EntryFee, MaxParticipants)
SELECT EventID, '12km Trail', 12.00, 220.00, 200 FROM Events WHERE EventName = 'Cape Town Trail Challenge';
INSERT INTO Categories (EventID, CategoryName, DistanceKm, EntryFee, MaxParticipants)
SELECT EventID, '25km Trail', 25.00, 350.00, 150 FROM Events WHERE EventName = 'Cape Town Trail Challenge';
GO

-- Sample enrolments (participants entering categories across all 3 events)
INSERT INTO Enrolments (ParticipantID, CategoryID, BibNumber, Status)
SELECT u.UserID, c.CategoryID, 'JHB-1001', 'Confirmed'
FROM Users u, Categories c
WHERE u.Email = 'amahle.nkosi@example.com' AND c.CategoryName = '10km Road Race'
  AND c.EventID = (SELECT EventID FROM Events WHERE EventName = 'Johannesburg City Marathon');

INSERT INTO Enrolments (ParticipantID, CategoryID, BibNumber, Status)
SELECT u.UserID, c.CategoryID, 'JHB-1002', 'Confirmed'
FROM Users u, Categories c
WHERE u.Email = 'jason.vanwyk@example.com' AND c.CategoryName = '21km Half Marathon'
  AND c.EventID = (SELECT EventID FROM Events WHERE EventName = 'Johannesburg City Marathon');

INSERT INTO Enrolments (ParticipantID, CategoryID, BibNumber, Status)
SELECT u.UserID, c.CategoryID, 'JHB-1003', 'Pending'
FROM Users u, Categories c
WHERE u.Email = 'palesa.mokoena@example.com' AND c.CategoryName = '5km Fun Run'
  AND c.EventID = (SELECT EventID FROM Events WHERE EventName = 'Johannesburg City Marathon');

INSERT INTO Enrolments (ParticipantID, CategoryID, BibNumber, Status)
SELECT u.UserID, c.CategoryID, 'DBN-2001', 'Confirmed'
FROM Users u, Categories c
WHERE u.Email = 'amahle.nkosi@example.com' AND c.CategoryName = '8km Beach Run'
  AND c.EventID = (SELECT EventID FROM Events WHERE EventName = 'Durban Beachfront Fun Run');

INSERT INTO Enrolments (ParticipantID, CategoryID, BibNumber, Status)
SELECT u.UserID, c.CategoryID, 'DBN-2002', 'Confirmed'
FROM Users u, Categories c
WHERE u.Email = 'ryan.govender@example.com' AND c.CategoryName = '3km Fun Run'
  AND c.EventID = (SELECT EventID FROM Events WHERE EventName = 'Durban Beachfront Fun Run');

INSERT INTO Enrolments (ParticipantID, CategoryID, BibNumber, Status)
SELECT u.UserID, c.CategoryID, 'CPT-3001', 'Confirmed'
FROM Users u, Categories c
WHERE u.Email = 'jason.vanwyk@example.com' AND c.CategoryName = '12km Trail'
  AND c.EventID = (SELECT EventID FROM Events WHERE EventName = 'Cape Town Trail Challenge');

INSERT INTO Enrolments (ParticipantID, CategoryID, BibNumber, Status)
SELECT u.UserID, c.CategoryID, 'CPT-3002', 'Withdrawn'
FROM Users u, Categories c
WHERE u.Email = 'ryan.govender@example.com' AND c.CategoryName = '25km Trail'
  AND c.EventID = (SELECT EventID FROM Events WHERE EventName = 'Cape Town Trail Challenge');
GO

-- Sample payments for the confirmed enrolments
INSERT INTO Payments (EnrolmentID, Amount, PaymentMethod, Status)
SELECT EnrolmentID, 180.00, 'Card', 'Paid' FROM Enrolments WHERE BibNumber = 'JHB-1001';
INSERT INTO Payments (EnrolmentID, Amount, PaymentMethod, Status)
SELECT EnrolmentID, 280.00, 'EFT', 'Paid' FROM Enrolments WHERE BibNumber = 'JHB-1002';
INSERT INTO Payments (EnrolmentID, Amount, PaymentMethod, Status)
SELECT EnrolmentID, 100.00, 'Card', 'Pending' FROM Enrolments WHERE BibNumber = 'JHB-1003';
INSERT INTO Payments (EnrolmentID, Amount, PaymentMethod, Status)
SELECT EnrolmentID, 120.00, 'Cash', 'Paid' FROM Enrolments WHERE BibNumber = 'DBN-2001';
INSERT INTO Payments (EnrolmentID, Amount, PaymentMethod, Status)
SELECT EnrolmentID, 60.00, 'Card', 'Paid' FROM Enrolments WHERE BibNumber = 'DBN-2002';
INSERT INTO Payments (EnrolmentID, Amount, PaymentMethod, Status)
SELECT EnrolmentID, 220.00, 'EFT', 'Paid' FROM Enrolments WHERE BibNumber = 'CPT-3001';
GO

-- Sample result for one past enrolment, to show the Enrolments -> Results relationship populated
INSERT INTO Results (EnrolmentID, FinishTimeSeconds, Position, Status)
SELECT EnrolmentID, 2415, 1, 'Finished' FROM Enrolments WHERE BibNumber = 'DBN-2002';
GO

/* ==========================================================================
   3. QUICK VERIFICATION QUERIES (optional - safe to run after seeding)
   ========================================================================== */

SELECT 'Users' AS TableName, COUNT(*) AS RowsSeeded FROM Users
UNION ALL SELECT 'Events', COUNT(*) FROM Events
UNION ALL SELECT 'Categories', COUNT(*) FROM Categories
UNION ALL SELECT 'Enrolments', COUNT(*) FROM Enrolments
UNION ALL SELECT 'Results', COUNT(*) FROM Results
UNION ALL SELECT 'Payments', COUNT(*) FROM Payments;
GO
