using Google.Protobuf.Protocol;
using Server.Game.Objects;
using Server.Game.Movement;
using Server.Game.Room;
using System;
using System.Collections.Generic;

public class GarenSkillHandler : IChampionSkillHandler
{
    public void HandleSkill(GameRoom room, GameObject caster, C_SkillCast skillPacket)
    {
        switch (skillPacket.SkillId)
        {
            case (int)SkillType.Gerneral:
                HandleBasicAttack(room, caster, skillPacket);
                break;
            case (int)SkillType.QSpell:
                HandleGarenQ(room, caster, skillPacket);
                break;
            case (int)SkillType.WSpell:
                CombatActionMovement.PlayPlaceholder(room, caster, skillPacket.SkillId);
                break;
            case (int)SkillType.ESpell:
                HandleGarenE(room, caster);
                break;
            case (int)SkillType.RSpell:
                HandleGarenR(room, caster, skillPacket);
                break;
            default:
                Console.WriteLine("[Server] Unknown Garen skill.");
                break;
        }
    }

    private void HandleBasicAttack(GameRoom room, GameObject caster, C_SkillCast skillPacket)
    {
        GameObject target = room.FindObject(skillPacket.TargetId);
        if (!IsValidTarget(caster, target)) return;
        if (!CombatActionMovement.TryBegin(room, caster, skillPacket.SkillId)) return;
        ApplyDamage(room, caster, target, 50, SkillType.Gerneral);
    }

    private void HandleGarenQ(GameRoom room, GameObject caster, C_SkillCast skillPacket)
    {
        GameObject target = room.FindObject(skillPacket.TargetId);
        if (!IsValidTarget(caster, target)) return;
        if (!CombatActionMovement.TryBegin(room, caster, skillPacket.SkillId)) return;
        ApplyDamage(room, caster, target, 100, SkillType.QSpell);
    }

    private void HandleGarenE(GameRoom room, GameObject caster)
    {
        if (!CombatActionMovement.TryBegin(room, caster, (int)SkillType.ESpell)) return;
        const int tickCount = 6;
        const int tickIntervalMs = 250;
        const float range = 3.0f;
        const int damagePerTick = 47;

        room.Broadcast(new S_SkillResult
        {
            CasterId = caster.Info.ObjectId,
            SkillId = (int)SkillType.ESpell
        });

        int remainingTicks = tickCount;
        Action applyTick = null;
        applyTick = () =>
        {
            if (caster.Info == null || caster.Info.Hp <= 0 ||
                !ReferenceEquals(room.FindObject(caster.Info.ObjectId), caster))
                return;

            var hitObjects = new List<ulong>();
            foreach (GameObject obj in room.GetObjectsInRange(caster.Info.Position.ToNumericsVector3(), range))
            {
                if (!IsValidTarget(caster, obj))
                    continue;
                ApplyDamage(room, caster, obj, damagePerTick, SkillType.ESpell, hitObjects);
            }

            remainingTicks--;
            if (remainingTicks > 0)
                room.PushAfter(tickIntervalMs, applyTick);
        };

        // Same authoritative multi-hit policy as Annie W: immediate first hit,
        // then deterministic server ticks while the caster may keep moving.
        applyTick();
    }
    private void HandleGarenR(GameRoom room, GameObject caster, C_SkillCast skillPacket)
    {
        GameObject target = room.FindObject(skillPacket.TargetId);
        if (!IsValidTarget(caster, target)) return;

        if (!CombatActionMovement.TryBegin(room, caster, skillPacket.SkillId)) return;
        float executeThreshold = target.Info.MaxHp * 0.2f;
        int finalDamage = target.Info.Hp <= executeThreshold ? target.Info.Hp : 150;
        ApplyDamage(room, caster, target, finalDamage, SkillType.RSpell);
    }

    private void ApplyDamage(GameRoom room, GameObject caster, GameObject target, int damage,
        SkillType skillType, List<ulong> hitList = null)
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
            if (target is Player deadPlayer)
                room.CancelPlayerMovement(deadPlayer);
            room.Broadcast(new S_Dead { TargetId = target.Info.ObjectId });
        }
    }

    private bool IsValidTarget(GameObject caster, GameObject target)
    {
        return target?.Info != null && caster?.Info != null && target.Info.Hp > 0 && target.Info.TeamId != caster.Info.TeamId;
    }
}