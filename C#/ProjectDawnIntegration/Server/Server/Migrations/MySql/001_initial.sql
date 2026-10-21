CREATE TABLE IF NOT EXISTS account (
 AccountDbId int NOT NULL AUTO_INCREMENT PRIMARY KEY,
 AccountName varchar(450) NULL,
 UNIQUE KEY IX_Account_AccountName(AccountName)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
CREATE TABLE IF NOT EXISTS player (
 PlayerDbId int NOT NULL AUTO_INCREMENT PRIMARY KEY,
 PlayerName varchar(450) NULL,
 Appearance varchar(512) NULL,
 AccountDbId int NOT NULL,
 Level int NOT NULL,Hp int NOT NULL,MaxHp int NOT NULL,Attack int NOT NULL,Speed float NOT NULL,TotalExp int NOT NULL,
 UNIQUE KEY IX_Player_PlayerName(PlayerName),KEY IX_Player_AccountDbId(AccountDbId),
 CONSTRAINT FK_Player_Account FOREIGN KEY(AccountDbId) REFERENCES account(AccountDbId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
CREATE TABLE IF NOT EXISTS item (
 ItemDbId int NOT NULL AUTO_INCREMENT PRIMARY KEY,
 TemplateId int NOT NULL,Count int NOT NULL,Slot int NOT NULL,Equipped tinyint(1) NOT NULL DEFAULT 0,OwnerDbId int NULL,
 KEY IX_Item_Owner_Slot(OwnerDbId,Slot),
 CONSTRAINT FK_Item_Player FOREIGN KEY(OwnerDbId) REFERENCES player(PlayerDbId),
 CONSTRAINT CK_Item_Count CHECK(Count>0),CONSTRAINT CK_Item_Slot CHECK(Slot>=0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
CREATE TABLE IF NOT EXISTS matchhistory (
 Id int NOT NULL AUTO_INCREMENT PRIMARY KEY,PartyId int NOT NULL,ServerRunId varchar(32) NULL,
 QueueKey longtext NULL,TargetRoomType longtext NULL,TargetRoomId int NULL,TransferId int NULL,
 CreatedAtUtc datetime(6) NOT NULL,TransferStartedAtUtc datetime(6) NULL,DungeonEnteredAtUtc datetime(6) NULL,
 ResultStatus longtext NULL,FailureReason longtext NULL,KEY IX_MatchHistory_PartyId(PartyId),UNIQUE KEY UX_MatchHistory_Run_Party(ServerRunId,PartyId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
CREATE TABLE IF NOT EXISTS matchhistorymember (
 Id int NOT NULL AUTO_INCREMENT PRIMARY KEY,MatchHistoryId int NOT NULL,PlayerId int NOT NULL,PlayerName longtext NULL,
 KEY IX_MatchHistoryMember_MatchHistoryId(MatchHistoryId),
 CONSTRAINT FK_MatchMember_History FOREIGN KEY(MatchHistoryId) REFERENCES matchhistory(Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
CREATE TABLE IF NOT EXISTS raidrewardencounter (
 Encounter bigint NOT NULL PRIMARY KEY,Digest varchar(64) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,CompletedUtc datetime(6) NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
CREATE TABLE IF NOT EXISTS raidrewardgrant (
 Encounter bigint NOT NULL,PlayerDbId int NOT NULL,Slot int NOT NULL,Name varchar(64) NOT NULL,
 Eligible tinyint(1) NOT NULL,Amount int NOT NULL,Reason varchar(64) NOT NULL,
 EquipmentTemplate int NOT NULL DEFAULT 0,EquipmentName varchar(128) NOT NULL DEFAULT '',EquipmentIcon varchar(256) NOT NULL DEFAULT '',EquipmentItemDbId int NULL,
 PRIMARY KEY(Encounter,PlayerDbId),UNIQUE KEY UX_Reward_Encounter_Slot(Encounter,Slot),
 UNIQUE KEY UX_Reward_Item(EquipmentItemDbId),KEY IX_Reward_Player_Encounter_Amount(PlayerDbId,Encounter,Amount),
 CONSTRAINT FK_Reward_Encounter FOREIGN KEY(Encounter) REFERENCES raidrewardencounter(Encounter),
 CONSTRAINT FK_Reward_Player FOREIGN KEY(PlayerDbId) REFERENCES player(PlayerDbId),
 CONSTRAINT FK_Reward_Item FOREIGN KEY(EquipmentItemDbId) REFERENCES item(ItemDbId),
 CONSTRAINT CK_Reward_Amount CHECK((Eligible=1 AND Amount=100) OR (Eligible=0 AND Amount=0)),
 CONSTRAINT CK_Reward_Slot CHECK(Slot BETWEEN 0 AND 3)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;
