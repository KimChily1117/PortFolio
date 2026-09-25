using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using Google.Protobuf;
using Google.Protobuf.Protocol;
using Server.Game.Navigation;
using Server.Game.Objects;
using Server.Game.Room;
using V3 = System.Numerics.Vector3;
using P3 = Google.Protobuf.Protocol.Vector3;

Directory.CreateDirectory("results");
using var report = new StreamWriter("results/server-range-report.txt");
int checks = 0;
void Check(bool condition, string label)
{
    report.WriteLine((condition ? "PASS " : "FAIL ") + label);
    report.Flush();
    if (!condition) throw new Exception(label);
    ++checks;
}
var inside = typeof(AnnieSkillHandler).GetMethod("IsInsideCone", BindingFlags.NonPublic | BindingFlags.Static);
bool InCone(float x, float z) => (bool)inside.Invoke(null, new object[] { V3.Zero, V3.UnitX, new V3(x, 0, z) });
Check(InCone(5.5f, 0), "expanded W includes target at 5.5");
Check(InCone(5.6f, 0), "W boundary at 5.6 included");
Check(!InCone(5.61f, 0), "target beyond 5.6 excluded");
Check(InCone(5f * MathF.Cos(24f * MathF.PI / 180f), 5f * MathF.Sin(24f * MathF.PI / 180f)), "24 degree target included");
Check(!InCone(5f * MathF.Cos(26f * MathF.PI / 180f), 5f * MathF.Sin(26f * MathF.PI / 180f)), "26 degree target excluded");
Check(!InCone(-1, 0), "target behind caster excluded");

// The production assembly keeps fixture constructors internal. Build the
// in-memory room without changing its public API or loading a live server.
var asset = (NavGridAsset)Activator.CreateInstance(typeof(NavGridAsset),
    BindingFlags.Instance | BindingFlags.NonPublic, null,
    new object[] { 1u, "annie-w-range", 32u, 32u, 1f, V3.Zero, .5f, 1u, new byte[32], new byte[32 * 32] }, null);
var config = new NavigationMapConfig { RoomId = 79001, SceneId = 0, NavigationMapId = asset.MapId,
    AssetPath = "tests/range.navgrid", FormatVersion = 1, ContentHashHex = asset.ContentHashHex, Enabled = true, Required = true };
var registration = (NavigationRegistration)Activator.CreateInstance(typeof(NavigationRegistration),
    BindingFlags.Instance | BindingFlags.NonPublic, null, new object[] { config, asset }, null);
var room = new GameRoom(registration);
Player Actor(ulong id, int team, float x, float z) => new Player { Info = new ObjectInfo {
    ObjectId = id, TeamId = team, Hp = 100, MaxHp = 100, ObjType = OBJECT_TYPE.Player,
    State = OBJECT_STATE_TYPE.Idle, Position = new P3 { X = x, Y = 2, Z = z } } };
var caster = Actor(1, 1, 10, 10);
caster.Info.ChampType = PLAYER_CHAMPION_TYPE.PlayerTypeAnnie;
var extendedEnemy = Actor(2, 2, 15.5f, 10);
var innerEnemy = Actor(3, 2, 14.8f, 12.1f);
var outside = Actor(4, 2, 15.7f, 10);
var side = Actor(5, 2, 14.5f, 13);
var ally = Actor(6, 1, 14.5f, 10);
foreach (var actor in new[] { caster, extendedEnemy, innerEnemy, outside, side, ally }) room.AddObject(actor);
room.Flush();
var packets = new List<IMessage>();
typeof(GameRoom).GetProperty("PacketBroadcastSink", BindingFlags.Instance | BindingFlags.NonPublic)
    .SetValue(room, (Action<IMessage>)packets.Add);
var handler = new AnnieSkillHandler();
handler.HandleSkill(room, caster, new C_SkillCast { CasterId = 1, SkillId = 2,
    IsAreaSkill = true, AreaRadius = 999f, TargetPos = new P3 { X = 15.5f, Y = 2, Z = 10 } });
room.Flush();
var cast = packets.OfType<S_SkillResult>().Single();
Check(MathF.Abs(cast.AreaRadius - 5.6f) < .0001f, "server advertises 5.6 and ignores client radius override");
Check(cast.CastOrigin.X == 10 && cast.CastDirection.X == 1, "server retains approved origin and direction");
// Damage must stay at the cast point after the caster moves.
caster.Info.Position.X = 20;
for (int i = 1; i < 10; ++i) { Thread.Sleep(210); room.Flush(); }
var damage = packets.OfType<S_Damage>().ToArray();
Check(extendedEnemy.Info.Hp == 70 && innerEnemy.Info.Hp == 70, "expanded region receives all ten ticks after caster movement");
Check(outside.Info.Hp == 100 && side.Info.Hp == 100 && ally.Info.Hp == 100,
    "range boundary, angle boundary and friendly exclusion unchanged");
Check(damage.Length == 20 && damage.All(p => p.Damage == 3), "two valid targets receive ten ticks of three damage each");
packets.Clear();
handler.HandleSkill(room, caster, new C_SkillCast { CasterId = 1, SkillId = 2,
    TargetPos = new P3 { X = 25.75f, Y = 2, Z = 10 } });
room.Flush();
Check(packets.Count == 0, "cast request outside range plus tolerance rejected");
report.WriteLine($"checks={checks}, failures=0");
Console.WriteLine($"Annie W server range: {checks} checks passed.");
