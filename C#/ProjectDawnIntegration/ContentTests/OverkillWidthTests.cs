using System;
using System.Linq;
using DnfRaidDemo;
using ProjectDawn.Combat;

internal static partial class Program
{
    static void TestOverkillWidth()
    {
        var simulation=ReferenceTraining();
        ReferenceCast(simulation,SkillId.ExtremeOverkill);
        ReferenceIdle(simulation,72);
        ReferenceCast(simulation,SkillId.ExtremeOverkill);
        var zone=simulation.Zones.First(z=>z.FromPlayer && z.Skill==(int)SkillId.ExtremeOverkill);
        var zero=RaidVector.zero;
        Check(RaidSimulation.Intersects(zone,zone.Center+new RaidVector(5.09f,0),zero)
            && RaidSimulation.Intersects(zone,zone.Center+new RaidVector(-5.09f,0),zero)
            && !RaidSimulation.Intersects(zone,zone.Center+new RaidVector(5.11f,0),zero),
            "Overkill ellipse reaches 5.1 units left and right, exactly 1.5 times its previous horizontal radius");
        Check(RaidSimulation.Intersects(zone,zone.Center+new RaidVector(0,3.39f),zero)
            && !RaidSimulation.Intersects(zone,zone.Center+new RaidVector(0,3.41f),zero)
            && !RaidSimulation.Intersects(zone,zone.Center+new RaidVector(4.8f,2f),zero),
            "Overkill keeps its 3.4 vertical radius and rejects the empty corners of the bounding box");
        Check(RaidSimulation.Intersects(zone,zone.Center+new RaidVector(5.8f,0),RaidSimulation.BossHurtboxHalfSize)
            && !RaidSimulation.Intersects(zone,zone.Center+new RaidVector(5.9f,0),RaidSimulation.BossHurtboxHalfSize),
            "Overkill scales ellipse versus boss hurtbox accurately at its new horizontal edge");
        var unrelated=zone.Clone();unrelated.Skill=(int)SkillId.BloodSnatch;
        Check(!RaidSimulation.Intersects(unrelated,zone.Center+new RaidVector(4.2f,0),zero),
            "horizontal widening does not alter other skills using circular footprints");
        simulation.Boss.Position=zone.Center+new RaidVector(5.5f,0);
        float before=simulation.Boss.Hp;
        var resumed=RaidSnapshotCodec.Decode(RaidSnapshotCodec.Encode(simulation));
        ReferenceIdle(simulation,240);ReferenceIdle(resumed,240);
        Check(Math.Abs(before-simulation.Boss.Hp-2145f)<.01f,
            "all ten authoritative Overkill hits damage a boss outside the former radius but inside the widened range");
        Check(RaidSnapshotCodec.Encode(simulation).SequenceEqual(RaidSnapshotCodec.Encode(resumed)),
            "checkpoint replay preserves widened Overkill collision results without client-provided range data");
    }
}
