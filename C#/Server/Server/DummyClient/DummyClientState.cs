namespace DummyClient
{
    public enum DummyClientState
    {
        Created,
        Connected,
        LoginSent,
        LoggedIn,
        CreatePlayerSent,
        PlayerReady,
        EnterGameSent,
        EnteredTown,
        MatchRequested,
        WaitingForExternal,
        PartyMatched,
        SceneMoveReceived,
        SceneReadySent,
        EnteredDungeon,
        GameplayStarted,
        GameplayCompleted,
        HoldingConnection,
        Completed,
        Failed
    }
}
