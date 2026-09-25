using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server;
using Server.Game.Movement;
using Server.Game.Navigation;
using Server.Game.Objects;
using Server.Game.Room;
using V3 = System.Numerics.Vector3;
using P3 = Google.Protobuf.Protocol.Vector3;

Directory.CreateDirectory("results");
using var report = new StreamWriter("results/server-movement-report.txt");
int checks = 0;
void Check(bool condition, string label)
{
    report.WriteLine((condition ? "PASS " : "FAIL ") + label); report.Flush();
    if (!condition) throw new Exception(label);
    ++checks;
}
const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
var deadline = typeof(Player).GetField("_combatActionEndsAt", hidden);
var asset = (NavGridAsset)Activator.CreateInstance(typeof(NavGridAsset), hidden, null,
    new object[] { 1u, "combat-movement", 32u, 32u, 1f, V3.Zero, .5f, 1u, new byte[32], new byte[1024] }, null);

(GameRoom room, Player player, ClientSession session, List<IMessage> packets, List<S_MovementSnapshot> snapshots) Fixture(PLAYER_CHAMPION_TYPE champ)
{
    var config = new NavigationMapConfig { RoomId = 79101, SceneId = 0, NavigationMapId = asset.MapId,
        AssetPath = "tests/combat.navgrid", FormatVersion = 1, ContentHashHex = asset.ContentHashHex, Enabled = true, Required = true };
    var registration = (NavigationRegistration)Activator.CreateInstance(typeof(NavigationRegistration), hidden, null, new object[] { config, asset }, null);
    var room = new GameRoom(registration);
    var packets = new List<IMessage>(); var snapshots = new List<S_MovementSnapshot>();
    typeof(GameRoom).GetProperty("PacketBroadcastSink", hidden).SetValue(room, (Action<IMessage>)packets.Add);
    typeof(GameRoom).GetProperty("MovementSnapshotSink", hidden).SetValue(room, (Action<S_MovementSnapshot>)snapshots.Add);
    var player = new Player { Info = new ObjectInfo { ObjectId = 1, TeamId = 1, Hp = 1000, MaxHp = 1000,
        ChampType = champ, ObjType = OBJECT_TYPE.Player, Position = new P3 { X = 10.5f, Y = 2, Z = 10.5f } } };
    var enemy = new Player { Info = new ObjectInfo { ObjectId = 2, TeamId = 2, Hp = 1000, MaxHp = 1000,
        ObjType = OBJECT_TYPE.Player, Position = new P3 { X = 11.5f, Y = 2, Z = 10.5f } } };
    room.AddObject(player); room.AddObject(enemy); room.Flush();
    var session = new ClientSession { Player = player, GameRoom = room };
    player.Session = session;
    return (room, player, session, packets, snapshots);
}
MoveRequestDecision Move(GameRoom room, Player player, ClientSession session, uint sequence, float x = 20.5f)
{
    return AuthoritativeMoveService.Process(session, room, player, new C_MoveRequest {
        ClientMoveSequence = sequence, NavigationMapId = room.NavigationMapId,
        NavigationContentHash = room.NavigationContentHash, RequestedDestinationX = x, RequestedDestinationZ = 10.5f });
}
void Cast(GameRoom room, Player player, int skillId, ulong targetId = 2, float targetX = 14.5f)
{
    IChampionSkillHandler handler = player.Info.ChampType == PLAYER_CHAMPION_TYPE.PlayerTypeAnnie
        ? new AnnieSkillHandler() : new GarenSkillHandler();
    handler.HandleSkill(room, player, new C_SkillCast { CasterId = 1, SkillId = skillId,
        TargetId = targetId, TargetPos = new P3 { X = targetX, Y = 2, Z = 10.5f } });
}
foreach (var champ in new[] { PLAYER_CHAMPION_TYPE.PlayerTypeAnnie, PLAYER_CHAMPION_TYPE.PlayerTypeGaren })
for (int skill = 0; skill <= 4; ++skill)
{
    var (room, player, session, packets, snapshots) = Fixture(champ);
    string label = champ + " skill " + skill;
    Check(Move(room, player, session, 1).Accepted, label + " starts with active movement");
    player.Update(.1f);
    var start = player.Info.Position.Clone();
    Cast(room, player, skill);
    bool mobile = champ == PLAYER_CHAMPION_TYPE.PlayerTypeGaren && skill == 3;
    Check(player.IsPerformingCombatAction && player.IsMovementLockedByCombat == !mobile, label + " correct action lock");
    Check(player.MovementState.IsActive == mobile && snapshots.Any(s => s.MovementState == MOVEMENT_SNAPSHOT_STATE.Cancelled) == !mobile,
        label + " existing path cancellation policy");
    Check(Move(room, player, session, 2, 21.5f).Accepted == mobile, label + " new move request policy");
    player.Update(.1f);
    Check((player.Info.Position.X != start.X) == mobile, label + " authoritative position policy");
    int resultCount = packets.OfType<S_SkillResult>().Count();
    Cast(room, player, 1);
    Check(packets.OfType<S_SkillResult>().Count() == resultCount, label + " another action cannot interrupt active action");

    // Compare the actual animation header rather than duplicating the duration constants.
    string clip = skill == 0 ? "Atk1" : skill == 1 ? "Qspell" : skill == 2 ?
        (champ == PLAYER_CHAMPION_TYPE.PlayerTypeGaren ? "Idle" : "Wspell") : skill == 3 ? "Espell" : "Rspell";
    string champion = champ == PLAYER_CHAMPION_TYPE.PlayerTypeAnnie ? "Annie" : "Garen";
    using var reader = new BinaryReader(File.OpenRead($"../../Resources/Models/{champion}/{clip}.clip"));
    reader.ReadBytes(reader.ReadInt32()); reader.ReadSingle();
    float rate = reader.ReadSingle(); uint frames = reader.ReadUInt32();
    double remaining = ((long)deadline.GetValue(player) - Stopwatch.GetTimestamp()) / (double)Stopwatch.Frequency;
    Check(Math.Abs(remaining - frames / (double)rate) < .25, label + " lock duration matches baked animation");
    // Expire only the clock; run the real movement service for unlock behavior.
    deadline.SetValue(player, Stopwatch.GetTimestamp() - 1);
    Check(!player.IsMovementLockedByCombat && Move(room, player, session, 3).Accepted, label + " movement resumes on a fresh click after action");
}
{
    var (room, player, session, packets, snapshots) = Fixture(PLAYER_CHAMPION_TYPE.PlayerTypeAnnie);
    Move(room, player, session, 1);
    Cast(room, player, 1, targetId: 999);
    Check(!player.IsPerformingCombatAction && player.MovementState.IsActive, "invalid Q target neither locks nor cancels movement");
    Cast(room, player, 2, targetX: 30);
    Check(!player.IsPerformingCombatAction && player.MovementState.IsActive, "out-of-range W neither locks nor cancels approach movement");
    Cast(room, player, 3);
    Thread.Sleep(620);
    Check(!player.IsMovementLockedByCombat && Move(room, player, session, 2).Accepted, "action expires naturally without another packet or release job");
    player.Info.Hp = 0;
    Check(!Move(room, player, session, 3).Accepted, "dead player cannot move after action expiry");
}
{
    var (room, player, session, packets, snapshots) = Fixture(PLAYER_CHAMPION_TYPE.PlayerTypeGaren);
    Move(room, player, session, 1);
    Cast(room, player, 3);
    for (int i = 1; i < 6; ++i) { Thread.Sleep(260); room.Flush(); }
    var damage = packets.OfType<S_Damage>().ToArray();
    Check(damage.Length == 6 && damage.All(d => d.Damage == 47), "Garen E retains six damage ticks of 47 while movement remains active");
    Check(player.MovementState.IsActive && !player.IsMovementLockedByCombat, "Garen E remains movable throughout its animation");
}
report.WriteLine($"checks={checks}, failures=0");
Console.WriteLine($"Combat movement server: {checks} checks passed.");
