-- NULL means no saved look yet. No existing rows are rewritten.
SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.Player', N'U') IS NULL
    THROW 50001, 'Existing Dawn Player table is required.', 1;
IF COL_LENGTH(N'dbo.Player', N'Appearance') IS NULL
    ALTER TABLE dbo.Player ADD Appearance nvarchar(512) NULL;
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id=OBJECT_ID(N'dbo.Player')
    AND name=N'Appearance' AND system_type_id=231 AND max_length=1024 AND is_nullable=1)
    THROW 50002, 'Unexpected Player.Appearance schema.', 1;
IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NOT NULL
    AND NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory WHERE MigrationId=N'20261018040000_AddPlayerAppearance')
    INSERT dbo.__EFMigrationsHistory(MigrationId,ProductVersion)
    VALUES(N'20261018040000_AddPlayerAppearance',N'3.1.7');
COMMIT;
