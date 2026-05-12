#!/bin/bash
set -e

echo "Creating database..."
/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P YourStrong!Passw0rd -C -Q "IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'Talaria') CREATE DATABASE Talaria" 2>/dev/null || true

echo "Creating tables..."
/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P YourStrong!Passw0rd -d Talaria -C -Q "
IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'BankStatements')
BEGIN
    CREATE TABLE BankStatements (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        AccountNumber nvarchar(50) NOT NULL,
        StatementDate datetime2 NOT NULL,
        S3Key nvarchar(500) NOT NULL,
        EncryptedDataKey varbinary(max) NOT NULL
    );
END

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'AccessTokens')
BEGIN
    CREATE TABLE AccessTokens (
        Id uniqueidentifier NOT NULL PRIMARY KEY,
        TokenValue nvarchar(64) NOT NULL,
        UserId nvarchar(100) NOT NULL,
        DocumentId uniqueidentifier NOT NULL,
        Status int NOT NULL,
        CreatedAt datetime2 NOT NULL,
        ExpiresAt datetime2 NOT NULL,
        UsedAt datetime2 NULL
    );
    CREATE INDEX IX_AccessTokens_TokenValue ON AccessTokens(TokenValue);
END
"

echo "Seeding 10 test statements..."
/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P YourStrong!Passw0rd -d Talaria -C -Q "
DELETE FROM BankStatements;

INSERT INTO BankStatements (Id, AccountNumber, StatementDate, S3Key, EncryptedDataKey)
VALUES 
  ('11111111-1111-1111-1111-111111111111', 'ACC001', DATEADD(day, -1, GETDATE()), 'statements/statement-1.pdf', 0x0100000000000000000000000000000000000000000000000000000000000000),
  ('22222222-2222-2222-2222-222222222222', 'ACC002', DATEADD(day, -2, GETDATE()), 'statements/statement-2.pdf', 0x0100000000000000000000000000000000000000000000000000000000000000),
  ('33333333-3333-3333-3333-333333333333', 'ACC003', DATEADD(day, -3, GETDATE()), 'statements/statement-3.pdf', 0x0100000000000000000000000000000000000000000000000000000000000000),
  ('44444444-4444-4444-4444-444444444444', 'ACC004', DATEADD(day, -4, GETDATE()), 'statements/statement-4.pdf', 0x0100000000000000000000000000000000000000000000000000000000000000),
  ('55555555-5555-5555-5555-555555555555', 'ACC005', DATEADD(day, -5, GETDATE()), 'statements/statement-5.pdf', 0x0100000000000000000000000000000000000000000000000000000000000000),
  ('66666666-6666-6666-6666-666666666666', 'ACC006', DATEADD(day, -6, GETDATE()), 'statements/statement-6.pdf', 0x0100000000000000000000000000000000000000000000000000000000000000),
  ('77777777-7777-7777-7777-777777777777', 'ACC007', DATEADD(day, -7, GETDATE()), 'statements/statement-7.pdf', 0x0100000000000000000000000000000000000000000000000000000000000000),
  ('88888888-8888-8888-8888-888888888888', 'ACC008', DATEADD(day, -8, GETDATE()), 'statements/statement-8.pdf', 0x0100000000000000000000000000000000000000000000000000000000000000),
  ('99999999-9999-9999-9999-999999999999', 'ACC009', DATEADD(day, -9, GETDATE()), 'statements/statement-9.pdf', 0x0100000000000000000000000000000000000000000000000000000000000000),
  ('aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa', 'ACC010', DATEADD(day, -10, GETDATE()), 'statements/statement-10.pdf', 0x0100000000000000000000000000000000000000000000000000000000000000)
"

echo "Verifying seed data..."
/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P YourStrong!Passw0rd -d Talaria -C -Q "SELECT Id, AccountNumber, S3Key FROM BankStatements"

echo "Database seeding complete"