-- =============================================
-- SQL Server Script: Create Sport Database with CDC
-- =============================================

-- =============================================
-- Create Debezium User for CDC
-- =============================================
USE master;
GO

-- Create login for Debezium
IF NOT EXISTS (SELECT name FROM sys.server_principals WHERE name = 'debezium_user')
BEGIN
    CREATE LOGIN debezium_user WITH PASSWORD = 'Debezium@2025!';
    PRINT 'Login [debezium_user] created';
END
ELSE
BEGIN
    PRINT 'Login [debezium_user] already exists';
END
GO

-- Create Sport Database
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'sport')
BEGIN
    CREATE DATABASE sport;
END
GO

USE sport;
GO

-- =============================================
-- Create Database User and Grant Permissions
-- =============================================
IF NOT EXISTS (SELECT name FROM sys.database_principals WHERE name = 'debezium_user')
BEGIN
    CREATE USER debezium_user FOR LOGIN debezium_user;
    PRINT 'User [debezium_user] created in [sport] database';
END
GO

-- Grant necessary permissions for Debezium CDC
ALTER ROLE db_datareader ADD MEMBER debezium_user;
GRANT VIEW DATABASE STATE TO debezium_user;
GRANT VIEW CHANGE TRACKING ON SCHEMA::dbo TO debezium_user;
PRINT 'Permissions granted to [debezium_user]';
GO

-- =============================================
-- Table: teams
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'teams')
BEGIN
    CREATE TABLE teams (
        team_id INT PRIMARY KEY IDENTITY(1,1),
        team_name NVARCHAR(100) NOT NULL,
        city NVARCHAR(100) NOT NULL,
        country NVARCHAR(100) NOT NULL,
        founded_year INT,
        stadium NVARCHAR(150),
        created_at DATETIME2 DEFAULT GETDATE(),
        updated_at DATETIME2 DEFAULT GETDATE()
    );
END
GO

-- =============================================
-- Table: players
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'players')
BEGIN
    CREATE TABLE players (
        player_id INT PRIMARY KEY IDENTITY(1,1),
        first_name NVARCHAR(50) NOT NULL,
        last_name NVARCHAR(50) NOT NULL,
        date_of_birth DATE,
        nationality NVARCHAR(100),
        position NVARCHAR(50),
        jersey_number INT,
        height_cm INT,
        weight_kg INT,
        created_at DATETIME2 DEFAULT GETDATE(),
        updated_at DATETIME2 DEFAULT GETDATE()
    );
END
GO

-- =============================================
-- Table: squad (links players to teams)
-- =============================================
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'squad')
BEGIN
    CREATE TABLE squad (
        squad_id INT PRIMARY KEY IDENTITY(1,1),
        team_id INT NOT NULL,
        player_id INT NOT NULL,
        join_date DATE NOT NULL,
        leave_date DATE,
        contract_value DECIMAL(15, 2),
        is_active BIT DEFAULT 1,
        created_at DATETIME2 DEFAULT GETDATE(),
        updated_at DATETIME2 DEFAULT GETDATE(),
        CONSTRAINT FK_squad_team FOREIGN KEY (team_id) REFERENCES teams(team_id) ON DELETE CASCADE,
        CONSTRAINT FK_squad_player FOREIGN KEY (player_id) REFERENCES players(player_id) ON DELETE CASCADE
    );
END
GO

-- =============================================
-- Initial Data: teams
-- =============================================
INSERT INTO teams (team_name, city, country, founded_year, stadium) VALUES
('Manchester United', 'Manchester', 'England', 1878, 'Old Trafford'),
('Real Madrid', 'Madrid', 'Spain', 1902, 'Santiago Bernabeu'),
('FC Barcelona', 'Barcelona', 'Spain', 1899, 'Camp Nou'),
('Bayern Munich', 'Munich', 'Germany', 1900, 'Allianz Arena'),
('Paris Saint-Germain', 'Paris', 'France', 1970, 'Parc des Princes'),
('Juventus', 'Turin', 'Italy', 1897, 'Allianz Stadium'),
('Liverpool', 'Liverpool', 'England', 1892, 'Anfield'),
('Chelsea', 'London', 'England', 1905, 'Stamford Bridge'),
('AC Milan', 'Milan', 'Italy', 1899, 'San Siro'),
('Arsenal', 'London', 'England', 1886, 'Emirates Stadium');
GO

-- =============================================
-- Initial Data: players
-- =============================================
INSERT INTO players (first_name, last_name, date_of_birth, nationality, position, jersey_number, height_cm, weight_kg) VALUES
('Marcus', 'Rashford', '1997-10-31', 'England', 'Forward', 10, 180, 70),
('Bruno', 'Fernandes', '1994-09-08', 'Portugal', 'Midfielder', 8, 179, 69),
('Jude', 'Bellingham', '2003-06-29', 'England', 'Midfielder', 5, 186, 75),
('Vinicius', 'Junior', '2000-07-12', 'Brazil', 'Forward', 7, 176, 73),
('Pedri', 'Gonzalez', '2002-11-25', 'Spain', 'Midfielder', 8, 174, 60),
('Robert', 'Lewandowski', '1988-08-21', 'Poland', 'Forward', 9, 185, 81),
('Harry', 'Kane', '1993-07-28', 'England', 'Forward', 9, 188, 86),
('Jamal', 'Musiala', '2003-02-26', 'Germany', 'Midfielder', 42, 183, 72),
('Kylian', 'Mbappe', '1998-12-20', 'France', 'Forward', 7, 178, 73),
('Ousmane', 'Dembele', '1997-05-15', 'France', 'Forward', 10, 178, 67),
('Federico', 'Chiesa', '1997-10-25', 'Italy', 'Forward', 7, 175, 70),
('Dusan', 'Vlahovic', '2000-01-28', 'Serbia', 'Forward', 9, 190, 80),
('Mohamed', 'Salah', '1992-06-15', 'Egypt', 'Forward', 11, 175, 71),
('Virgil', 'van Dijk', '1991-07-08', 'Netherlands', 'Defender', 4, 193, 92),
('Cole', 'Palmer', '2002-05-06', 'England', 'Midfielder', 20, 189, 70),
('Enzo', 'Fernandez', '2001-01-17', 'Argentina', 'Midfielder', 8, 178, 75),
('Rafael', 'Leao', '1999-06-10', 'Portugal', 'Forward', 10, 188, 81),
('Theo', 'Hernandez', '1997-10-06', 'France', 'Defender', 19, 184, 81),
('Bukayo', 'Saka', '2001-09-05', 'England', 'Forward', 7, 178, 72),
('Martin', 'Odegaard', '1998-12-17', 'Norway', 'Midfielder', 8, 178, 68);
GO

-- =============================================
-- Initial Data: squad
-- =============================================
INSERT INTO squad (team_id, player_id, join_date, leave_date, contract_value, is_active) VALUES
-- Manchester United
(1, 1, '2016-01-01', NULL, 20000000.00, 1),
(1, 2, '2020-01-30', NULL, 55000000.00, 1),
-- Real Madrid
(2, 3, '2023-06-14', NULL, 103000000.00, 1),
(2, 4, '2018-07-20', NULL, 45000000.00, 1),
-- FC Barcelona
(3, 5, '2020-09-01', NULL, 5000000.00, 1),
(3, 6, '2022-07-19', NULL, 45000000.00, 1),
-- Bayern Munich
(4, 7, '2023-08-12', NULL, 100000000.00, 1),
(4, 8, '2020-07-01', NULL, 500000.00, 1),
-- Paris Saint-Germain
(5, 9, '2017-08-31', '2024-06-30', 180000000.00, 0),
(5, 10, '2023-08-13', NULL, 50000000.00, 1),
-- Juventus
(6, 11, '2022-08-27', NULL, 3000000.00, 1),
(6, 12, '2022-01-28', NULL, 70000000.00, 1),
-- Liverpool
(7, 13, '2017-06-22', NULL, 36900000.00, 1),
(7, 14, '2018-01-01', NULL, 75000000.00, 1),
-- Chelsea
(8, 15, '2023-09-01', NULL, 40000000.00, 1),
(8, 16, '2023-02-01', NULL, 121000000.00, 1),
-- AC Milan
(9, 17, '2019-08-01', NULL, 35000000.00, 1),
(9, 18, '2019-07-06', NULL, 20000000.00, 1),
-- Arsenal
(10, 19, '2018-09-01', NULL, 0.00, 1),
(10, 20, '2021-08-20', NULL, 35000000.00, 1);
GO

-- =============================================
-- ENABLE CDC ON DATABASE
-- =============================================
-- Check if CDC is already enabled on the database
IF NOT EXISTS (SELECT 1 FROM sys.databases WHERE name = 'sport' AND is_cdc_enabled = 1)
BEGIN
    EXEC sys.sp_cdc_enable_db;
    PRINT 'CDC enabled on database [sport]';
END
ELSE
BEGIN
    PRINT 'CDC is already enabled on database [sport]';
END
GO

-- =============================================
-- ENABLE CDC ON TABLE: teams
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.tables t 
               JOIN cdc.change_tables ct ON t.object_id = ct.source_object_id 
               WHERE t.name = 'teams')
BEGIN
    EXEC sys.sp_cdc_enable_table
        @source_schema = N'dbo',
        @source_name = N'teams',
        @role_name = NULL,
        @supports_net_changes = 1;
    PRINT 'CDC enabled on table [dbo].[teams]';
END
ELSE
BEGIN
    PRINT 'CDC is already enabled on table [dbo].[teams]';
END
GO

-- =============================================
-- ENABLE CDC ON TABLE: players
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.tables t 
               JOIN cdc.change_tables ct ON t.object_id = ct.source_object_id 
               WHERE t.name = 'players')
BEGIN
    EXEC sys.sp_cdc_enable_table
        @source_schema = N'dbo',
        @source_name = N'players',
        @role_name = NULL,
        @supports_net_changes = 1;
    PRINT 'CDC enabled on table [dbo].[players]';
END
ELSE
BEGIN
    PRINT 'CDC is already enabled on table [dbo].[players]';
END
GO

-- =============================================
-- ENABLE CDC ON TABLE: squad
-- =============================================
IF NOT EXISTS (SELECT 1 FROM sys.tables t 
               JOIN cdc.change_tables ct ON t.object_id = ct.source_object_id 
               WHERE t.name = 'squad')
BEGIN
    EXEC sys.sp_cdc_enable_table
        @source_schema = N'dbo',
        @source_name = N'squad',
        @role_name = NULL,
        @supports_net_changes = 1;
    PRINT 'CDC enabled on table [dbo].[squad]';
END
ELSE
BEGIN
    PRINT 'CDC is already enabled on table [dbo].[squad]';
END
GO

-- =============================================
-- VERIFY CDC STATUS
-- =============================================
-- Check database CDC status
SELECT name, is_cdc_enabled 
FROM sys.databases 
WHERE name = 'sport';

-- Check tables with CDC enabled
SELECT 
    t.name AS table_name,
    ct.capture_instance,
    ct.start_lsn,
    ct.create_date
FROM sys.tables t
JOIN cdc.change_tables ct ON t.object_id = ct.source_object_id
ORDER BY t.name;

-- =============================================
-- GRANT CDC PERMISSIONS TO DEBEZIUM USER
-- =============================================
-- Grant SELECT on CDC schema tables
GRANT SELECT ON SCHEMA::cdc TO debezium_user;

-- Grant EXECUTE on CDC functions
GRANT EXECUTE ON SCHEMA::cdc TO debezium_user;

-- Grant SELECT on CDC system tables
GRANT SELECT ON cdc.change_tables TO debezium_user;
GRANT SELECT ON cdc.captured_columns TO debezium_user;
GRANT SELECT ON cdc.index_columns TO debezium_user;
GRANT SELECT ON cdc.lsn_time_mapping TO debezium_user;

PRINT 'CDC permissions granted to [debezium_user]';
GO

PRINT '========================================';
PRINT 'CDC Setup Complete!';
PRINT 'Database: sport';
PRINT 'Tables with CDC: teams, players, squad';
PRINT 'Debezium User: debezium_user';
PRINT 'Password: Debezium@2025!';
PRINT '========================================';
GO

