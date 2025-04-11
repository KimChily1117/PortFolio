//using Google.Protobuf.Protocol;
//using Server.Game.Objects;
//using Server.Game.Room;
//using System;
//using System.Collections.Generic;
//using System.Threading.Tasks;

//public class GarenSkillHandler : IChampionSkillHandler
//{
//    public void HandleSkill(GameRoom room, GameObject caster, C_SkillCast skillPacket)
//    {
//        switch (skillPacket.SkillId)
//        {
//            case (int)SkillType.Gerneral:
//                HandleBasicAttack(room, caster, skillPacket);
//                break;

//            case (int)SkillType.QSpell:
//                HandleGarenQ(room, caster, skillPacket);
//                break;

//            case (int)SkillType.ESpell:
//                HandleGarenE(room, caster);
//                break;

//            case (int)SkillType.RSpell:
//                HandleGarenR(room, caster, skillPacket);
//                break;

//            default:
//                Console.WriteLine("[Server] ❌ Unknown Garen skill.");
//                break;
//        }
//    }

//    #region General Attack
//    private void HandleBasicAttack(GameRoom room, GameObject caster, C_SkillCast skillPacket)
//    {
//        GameObject target = room.FindObject(skillPacket.TargetId);
//        if (!IsValidTarget(caster, target)) return;

//        int damage = 50;
//        ApplyDamage(room, caster, target, damage, SkillType.Gerneral);
//    }
//    #endregion

//    #region Garen Q
//    private void HandleGarenQ(GameRoom room, GameObject caster, C_SkillCast skillPacket)
//    {
//        GameObject target = room.FindObject(skillPacket.TargetId);
//        if (!IsValidTarget(caster, target)) return;

//        int damage = 100;
//        ApplyDamage(room, caster, target, damage, SkillType.QSpell);
//    }
//    #endregion

//    #region Garen E (AOE Tick Damage)
//    private void HandleGarenE(GameRoom room, GameObject caster)
//    {
//        int tickCount = 6;
//        float tickInterval = 0.5f;
//        float range = 3.0f;
//        int damagePerTick = 47;

//        ApplyGarenEDamage(room, caster, tickCount, tickInterval, range, damagePerTick);
//    }

//    private async void ApplyGarenEDamage(GameRoom room, GameObject caster, int tickCount, float interval, float range, int damage)
//    {
//        for (int i = 0; i < tickCount; i++)
//        {
//            await Task.Delay(TimeSpan.FromSeconds(interval));

//            List<ulong> hitObjects = new List<ulong>();

//            foreach (var obj in room.GetObjectsInRange(caster.Info.Position.ToNumericsVector3(), range))
//            {
//                if (!IsValidTarget(caster, obj)) continue;

//                ApplyDamage(room, caster, obj, damage, SkillType.ESpell, hitObjects);
//            }

//            if (hitObjects.Count > 0)
//            {
//                S_SkillResult skillResultPacket = new S_SkillResult
//                {
//                    CasterId = caster.Info.ObjectId,
//                    SkillId = (int)SkillType.ESpell,
//                    HitObjects = { hitObjects }
//                };
//                room.Broadcast(skillResultPacket);
//            }
//        }
//    }
//    #endregion

//    #region Garen R (Execution)
//    private void HandleGarenR(GameRoom room, GameObject caster, C_SkillCast skillPacket)
//    {
//        GameObject target = room.FindObject(skillPacket.TargetId);
//        if (!IsValidTarget(caster, target)) return;

//        float executeThreshold = target.Info.MaxHp * 0.2f;
//        int baseDamage = 150;

//        bool isExecute = target.Info.Hp <= executeThreshold;
//        int finalDamage = isExecute ? target.Info.Hp : baseDamage;

//        ApplyDamage(room, caster, target, finalDamage, SkillType.RSpell);
//    }
//    #endregion

//    #region 공통 Damage 처리
//    private void ApplyDamage(GameRoom room, GameObject caster, GameObject target, int damage, SkillType skillType, List<ulong> hitList = null)
//    {
//        target.Info.Hp -= damage;
//        target.Info.Hp = Math.Max(0, target.Info.Hp);

//        // 데미지 결과 브로드캐스트
//        S_Damage dmgPacket = new S_Damage
//        {
//            TargetId = target.Info.ObjectId,
//            Damage = damage,
//            RemainHp = target.Info.Hp
//        };
//        room.Broadcast(dmgPacket);

//        if (hitList != null)
//            hitList.Add(target.Info.ObjectId);
//        else
//        {
//            S_SkillResult result = new S_SkillResult
//            {
//                CasterId = caster.Info.ObjectId,
//                SkillId = (int)skillType,
//                HitObjects = { target.Info.ObjectId }
//            };
//            room.Broadcast(result);
//        }

//        // 사망 처리
//        if (target.Info.Hp <= 0)
//        {
//            Console.WriteLine($"[Server] 💀 {target.Info.ObjectId} has died.");
//            S_Dead deadPacket = new S_Dead
//            {
//                TargetId = target.Info.ObjectId
//            };
//            room.Broadcast(deadPacket);
//        }
//    }

//    private bool IsValidTarget(GameObject caster, GameObject target)
//    {
//        return target != null && caster != null &&
//               target.Info.TeamId != caster.Info.TeamId;
//    }
//    #endregion
//}

using Google.Protobuf.Protocol;
using Server.Game.Objects;
using Server.Game.Room;
using System.Collections.Generic;
using System;

public class GarenSkillHandler : IChampionSkillHandler
{
    public void HandleSkill(GameRoom room, GameObject caster, C_SkillCast skillPacket)
    {
        switch (skillPacket.SkillId)
        {
            case (int)SkillType.Gerneral:
                room.Push(() => HandleBasicAttack(room, caster, skillPacket));
                break;

            case (int)SkillType.QSpell:
                room.Push(() => HandleGarenQ(room, caster, skillPacket));
                break;

            case (int)SkillType.ESpell:
                room.Push(() => HandleGarenE(room, caster));
                break;

            case (int)SkillType.RSpell:
                room.Push(() => HandleGarenR(room, caster, skillPacket));
                break;

            default:
                Console.WriteLine("[Server] ❌ Unknown Garen skill.");
                break;
        }
    }

    private void HandleBasicAttack(GameRoom room, GameObject caster, C_SkillCast skillPacket)
    {
        GameObject target = room.FindObject(skillPacket.TargetId);
        if (!IsValidTarget(caster, target)) return;

        ApplyDamage(room, caster, target, 50, SkillType.Gerneral);
    }

    private void HandleGarenQ(GameRoom room, GameObject caster, C_SkillCast skillPacket)
    {
        GameObject target = room.FindObject(skillPacket.TargetId);
        if (!IsValidTarget(caster, target)) return;

        ApplyDamage(room, caster, target, 100, SkillType.QSpell);
    }

    private void HandleGarenE(GameRoom room, GameObject caster)
    {
        int tickCount = 6;
        int tickIntervalMs = 500;
        float range = 3.0f;
        int damagePerTick = 47;

        // ✅ 스킬 시작 애니메이션 → 한번만 전송
        room.Broadcast(new S_SkillResult
        {
            CasterId = caster.Info.ObjectId,
            SkillId = (int)SkillType.ESpell
        });

        // ✅ 틱마다 데미지 처리
        for (int i = 0; i < tickCount; i++)
        {
            int delay = tickIntervalMs * i;

            room.PushAfter(delay, () =>
            {
                foreach (var obj in room.GetObjectsInRange(caster.Info.Position.ToNumericsVector3(), range))
                {
                    if (!IsValidTarget(caster, obj)) continue;

                    ApplyDamage(room, caster, obj, damagePerTick, SkillType.ESpell);
                }
            });
        }
    }


    private void HandleGarenR(GameRoom room, GameObject caster, C_SkillCast skillPacket)
    {
        GameObject target = room.FindObject(skillPacket.TargetId);
        if (!IsValidTarget(caster, target)) return;

        float executeThreshold = target.Info.MaxHp * 0.2f;
        int baseDamage = 150;

        int finalDamage = (target.Info.Hp <= executeThreshold) ? target.Info.Hp : baseDamage;

        ApplyDamage(room, caster, target, finalDamage, SkillType.RSpell);
    }

    private void ApplyDamage(GameRoom room, GameObject caster, GameObject target, int damage, SkillType skillType, List<ulong> hitList = null)
    {
        target.Info.Hp = Math.Max(0, target.Info.Hp - damage);

        if (hitList != null)
            hitList.Add(target.Info.ObjectId);
        else
        {
            room.Broadcast(new S_SkillResult
            {
                CasterId = caster.Info.ObjectId,
                SkillId = (int)skillType,
                HitObjects = { target.Info.ObjectId }
            });
        }

        room.Broadcast(new S_Damage
        {
            TargetId = target.Info.ObjectId,
            Damage = damage,
            RemainHp = target.Info.Hp
        });

        if (target.Info.Hp <= 0)
        {
            room.Broadcast(new S_Dead { TargetId = target.Info.ObjectId });
        }
    }

    private bool IsValidTarget(GameObject caster, GameObject target)
    {
        return target != null && caster != null && target.Info.TeamId != caster.Info.TeamId;
    }
}
