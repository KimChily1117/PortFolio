IF OBJECT_ID(N'[dbo].[MatchHistoryMember]', N'U') IS NULL
BEGIN
    IF OBJECT_ID(N'[dbo].[MatchHistory]', N'U') IS NULL
    BEGIN
        CREATE TABLE [dbo].[MatchHistory] (
            [Id] int NOT NULL IDENTITY,
            [PartyId] int NOT NULL,
            [QueueKey] nvarchar(max) NULL,
            [TargetRoomType] nvarchar(max) NULL,
            [TargetRoomId] int NULL,
            [TransferId] int NULL,
            [CreatedAtUtc] datetime2 NOT NULL,
            [TransferStartedAtUtc] datetime2 NULL,
            [DungeonEnteredAtUtc] datetime2 NULL,
            [ResultStatus] nvarchar(max) NULL,
            [FailureReason] nvarchar(max) NULL,
            CONSTRAINT [PK_MatchHistory] PRIMARY KEY ([Id])
        );
        CREATE INDEX [IX_MatchHistory_PartyId] ON [dbo].[MatchHistory] ([PartyId]);
    END;

    CREATE TABLE [dbo].[MatchHistoryMember] (
        [Id] int NOT NULL IDENTITY,
        [MatchHistoryId] int NOT NULL,
        [PlayerId] int NOT NULL,
        [PlayerName] nvarchar(max) NULL,
        CONSTRAINT [PK_MatchHistoryMember] PRIMARY KEY ([Id]),
        CONSTRAINT [FK_MatchHistoryMember_MatchHistory_MatchHistoryId] FOREIGN KEY ([MatchHistoryId]) REFERENCES [dbo].[MatchHistory] ([Id]) ON DELETE CASCADE
    );
    CREATE INDEX [IX_MatchHistoryMember_MatchHistoryId] ON [dbo].[MatchHistoryMember] ([MatchHistoryId]);
END;