SET XACT_ABORT ON;
BEGIN TRANSACTION;
IF OBJECT_ID(N'dbo.RaidRewardEncounter',N'U') IS NULL
CREATE TABLE dbo.RaidRewardEncounter (
 Encounter bigint NOT NULL CONSTRAINT PK_RaidRewardEncounter PRIMARY KEY,
 Digest varchar(64) NOT NULL,
 CompletedUtc datetime2 NOT NULL
);
IF OBJECT_ID(N'dbo.RaidRewardGrant',N'U') IS NULL
BEGIN
 CREATE TABLE dbo.RaidRewardGrant (
  Encounter bigint NOT NULL,
  PlayerDbId int NOT NULL,
  Slot int NOT NULL,
  Name nvarchar(64) NOT NULL,
  Eligible bit NOT NULL,
  Amount int NOT NULL,
  Reason nvarchar(64) NOT NULL,
  CONSTRAINT PK_RaidRewardGrant PRIMARY KEY(Encounter,PlayerDbId),
  CONSTRAINT FK_RaidRewardGrant_Encounter FOREIGN KEY(Encounter) REFERENCES dbo.RaidRewardEncounter(Encounter),
  CONSTRAINT FK_RaidRewardGrant_Player FOREIGN KEY(PlayerDbId) REFERENCES dbo.Player(PlayerDbId),
  CONSTRAINT CK_RaidRewardGrant_Amount CHECK ((Eligible=1 AND Amount=100) OR (Eligible=0 AND Amount=0)),
  CONSTRAINT CK_RaidRewardGrant_Slot CHECK(Slot BETWEEN 0 AND 3)
 );
 CREATE INDEX IX_RaidRewardGrant_Player ON dbo.RaidRewardGrant(PlayerDbId,Encounter) INCLUDE(Amount);
END;
IF COL_LENGTH('dbo.RaidRewardGrant','EquipmentTemplate') IS NULL
 ALTER TABLE dbo.RaidRewardGrant ADD EquipmentTemplate int NOT NULL CONSTRAINT DF_RaidRewardGrant_Template DEFAULT(0),
 EquipmentName nvarchar(128) NOT NULL CONSTRAINT DF_RaidRewardGrant_EquipmentName DEFAULT(''),
 EquipmentIcon nvarchar(256) NOT NULL CONSTRAINT DF_RaidRewardGrant_Icon DEFAULT(''), EquipmentItemDbId int NULL;
COMMIT;
