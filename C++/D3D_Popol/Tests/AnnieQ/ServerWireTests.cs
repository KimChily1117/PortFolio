using System;
using System.IO;
using Google.Protobuf;
using Google.Protobuf.Protocol;

Directory.CreateDirectory("results");
foreach (var kind in new[] { "basic", "q", "legacy" })
{
    var packet = new S_ProjectileSpawn { ProjectileId = 123, CasterId = 1, TargetId = 2, Speed = 6.55f };
    if (kind != "legacy") packet.SkillId = kind == "q" ? 1 : 0;
    var bytes = packet.ToByteArray();
    var parsed = S_ProjectileSpawn.Parser.ParseFrom(bytes);
    if (parsed.HasSkillId != (kind != "legacy") || parsed.SkillId != (kind == "q" ? 1 : 0))
        throw new Exception("Optional skillId roundtrip failed: " + kind);
    File.WriteAllBytes("results/server-" + kind + ".packet", bytes);
}
Console.WriteLine("C# server: basic/Q/legacy serialization passed; wrote C++ interoperability fixtures.");
