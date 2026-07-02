-- setup.sql
-- One-time SQL script to create the database and table for Timezone Manager.
-- Run this before starting the app (see CLAUDE.md setup step 4).
-- Usage: sqlcmd -S localhost -U sa -P "YourPassword123!" -i setup.sql

CREATE DATABASE TimezoneManager;
GO

USE TimezoneManager;
GO

CREATE TABLE DeliveryRecords (
    Id                  INT           IDENTITY(1,1)  PRIMARY KEY,
    DeliveryNumber      BIGINT        NOT NULL,
    Plant               NVARCHAR(50)  NOT NULL,
    Material            NVARCHAR(100) NOT NULL,
    -- datetime2(7) is the modern SQL Server timestamp type. Always store UTC here.
    TimestampUtc        datetime2(7)  NOT NULL,
    -- IANA timezone ID of the person who submitted the record, e.g. "Europe/Stockholm".
    SubmitterTimezone   NVARCHAR(100) NOT NULL
);
GO
