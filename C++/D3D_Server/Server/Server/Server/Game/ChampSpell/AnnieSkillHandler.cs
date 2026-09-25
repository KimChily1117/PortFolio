using Google.Protobuf.Protocol;
using Server.Game;
using Server.Game.Movement;
using Server.Game.Objects;
using Server.Game.Room;
using System;
using Vec3 = System.Numerics.Vector3;

public class AnnieSkillHandler : IChampionSkillHandler
{
    private const float AnnieWRange = 5.6f;
    private const float AnnieWHalfAngleDegrees = 25.0f;

    public void HandleSkill(GameRoom room, GameObject caster, C_SkillCast skillPacket)
    {
        switch (skillPacket.SkillId)
        {
            case (int)SkillType.Gerneral:
                HandleBasicAttack(room, caster, skillPacket);
                break;
            case (int)SkillType.QSpell:
                HandleAnnieQ(room, caster, skillPacket);
                break;
            case (int)SkillType.WSpell:
                HandleAnnieW(room, caster, skillPacket);
                break;
            case (int)SkillType.ESpell:
            case (int)SkillType.RSpell:
                CombatActionMovement.PlayPlaceholder(room, caster, skillPacket.SkillId);
                break;
        }
    }

    private void HandleBasicAttack(GameRoom room, GameObject caster, C_SkillCast skillPacket)
    {
        GameObject target = room.FindObject(skillPacket.TargetId);
        if (!IsValidTarget(caster, target)) return;

        if (!CombatActionMovement.TryBegin(room, caster, skillPacket.SkillId)) return;
        const int damage = 40;
        ulong projectileId = room.GenerateProjectileId();
        Projectile projectile = new Projectile(projectileId, caster, target, 3.75f, damage);
        room.AddProjectile(projectile);

        room.Broadcast(new S_SkillResult
        {
            CasterId = caster.Info.ObjectId,
            SkillId = (int)SkillType.Gerneral
        });
        room.Broadcast(new S_ProjectileSpawn
        {
            ProjectileId = projectileId,
            CasterId = caster.Info.ObjectId,
            TargetId = target.Info.ObjectId,
            StartPos = caster.Info.Position,
            EndPos = target.Info.Position,
            Speed = 3.75f,
            SkillId = (int)SkillType.Gerneral
        });
    }

    private void HandleAnnieQ(GameRoom room, GameObject caster, C_SkillCast skillPacket)
    {
        GameObject target = room.FindObject(skillPacket.TargetId);
        if (!IsValidTarget(caster, target)) return;

        if (!CombatActionMovement.TryBegin(room, caster, skillPacket.SkillId)) return;
        const int damage = 65;
        ulong projectileId = room.GenerateProjectileId();
        Projectile projectile = new Projectile(projectileId, caster, target, 6.55f, damage);
        room.AddProjectile(projectile);

        room.Broadcast(new S_SkillResult
        {
            CasterId = caster.Info.ObjectId,
            SkillId = (int)SkillType.QSpell
        });
        room.Broadcast(new S_ProjectileSpawn
        {
            ProjectileId = projectileId,
            CasterId = caster.Info.ObjectId,
            TargetId = target.Info.ObjectId,
            StartPos = caster.Info.Position,
            EndPos = target.Info.Position,
            Speed = 6.55f,
            SkillId = (int)SkillType.QSpell
        });
    }

    private void HandleAnnieW(GameRoom room, GameObject caster, C_SkillCast skillPacket)
    {
        const int tickCount = 10;
        const int tickIntervalMs = 200;
        const int damagePerTick = 3;

        if (caster == null || caster.Info == null || skillPacket.TargetPos == null)
            return;

        Vec3 castOrigin = caster.Info.Position.ToNumericsVector3();
        Vec3 targetPosition = new Vec3(skillPacket.TargetPos.X, skillPacket.TargetPos.Y, skillPacket.TargetPos.Z);
        if (!IsFinite(castOrigin) || !IsFinite(targetPosition))
            return;

        Vec3 castDirection = targetPosition - castOrigin;
        castDirection.Y = 0.0f;
        float requestedDistance = castDirection.Length();
        if (!IsFinite(requestedDistance) || requestedDistance <= 0.01f || requestedDistance > AnnieWRange + 0.10f)
            return;
        castDirection /= requestedDistance;
        if (!CombatActionMovement.TryBegin(room, caster, skillPacket.SkillId)) return;

        room.Broadcast(new S_SkillResult
        {
            CasterId = caster.Info.ObjectId,
            SkillId = (int)SkillType.WSpell,
            IsAreaSkill = true,
            AreaRadius = AnnieWRange,
            CenterPos = ToProtocolVector(castOrigin),
            CastOrigin = ToProtocolVector(castOrigin),
            CastDirection = ToProtocolVector(castDirection)
        });

        int remainingTicks = tickCount;
        Action applyTick = null;
        applyTick = () =>
        {
            if (caster.Info == null || caster.Info.Hp <= 0 ||
                !ReferenceEquals(room.FindObject(caster.Info.ObjectId), caster))
                return;

            foreach (GameObject target in room.GetObjectsInRange(castOrigin, AnnieWRange))
            {
                if (!IsValidTarget(caster, target) || !IsInsideCone(castOrigin, castDirection, target.Info.Position.ToNumericsVector3()))
                    continue;

                target.Info.Hp = Math.Max(0, target.Info.Hp - damagePerTick);
                room.Broadcast(new S_Damage
                {
                    TargetId = target.Info.ObjectId,
                    Damage = damagePerTick,
                    RemainHp = target.Info.Hp
                });

                if (target.Info.Hp <= 0)
                {
                    Player deadPlayer = target as Player;
                    if (deadPlayer != null)
                        room.CancelPlayerMovement(deadPlayer);
                    room.Broadcast(new S_Dead { TargetId = target.Info.ObjectId });
                }
            }

            remainingTicks--;
            if (remainingTicks > 0)
                room.PushAfter(tickIntervalMs, applyTick);
        };

        applyTick();
    }

    private static bool IsInsideCone(Vec3 origin, Vec3 direction, Vec3 targetPosition)
    {
        Vec3 offset = targetPosition - origin;
        offset.Y = 0.0f;
        float distanceSquared = offset.LengthSquared();
        if (!IsFinite(distanceSquared) || distanceSquared > AnnieWRange * AnnieWRange)
            return false;
        if (distanceSquared <= 0.0001f)
            return true;

        offset /= (float)Math.Sqrt(distanceSquared);
        float minimumDot = (float)Math.Cos(AnnieWHalfAngleDegrees * Math.PI / 180.0);
        return Vec3.Dot(direction, offset) >= minimumDot;
    }

    private static bool IsValidTarget(GameObject caster, GameObject target)
    {
        return target != null && target != caster && target.Info != null && target.Info.Hp > 0 &&
            caster.Info != null && target.Info.TeamId != caster.Info.TeamId;
    }

    private static bool IsFinite(Vec3 value)
    {
        return IsFinite(value.X) && IsFinite(value.Y) && IsFinite(value.Z);
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private static Google.Protobuf.Protocol.Vector3 ToProtocolVector(Vec3 value)
    {
        return new Google.Protobuf.Protocol.Vector3 { X = value.X, Y = value.Y, Z = value.Z };
    }
}

