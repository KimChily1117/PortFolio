using Google.Protobuf.Protocol;
using Server.Game;
using Server.Game.Objects;
using Server.Game.Room;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Vec3 = System.Numerics.Vector3;

public class AnnieSkillHandler : IChampionSkillHandler
{
    public void HandleSkill(GameRoom room, GameObject caster, C_SkillCast skillPacket)
    {
        switch (skillPacket.SkillId)
        {
            case (int)SkillType.Gerneral:
                room.Push(() => HandleBasicAttack(room, caster, skillPacket));
                break;
            case (int)SkillType.QSpell:
                room.Push(() => HandleAnnieQ(room, caster, skillPacket));
                break;
            case (int)SkillType.WSpell:
                room.Push(() => HandleAnnieW(room, caster));
                break;
        }
    }

    private void HandleBasicAttack(GameRoom room, GameObject caster, C_SkillCast skillPacket)
    {
        GameObject target = room.FindObject(skillPacket.TargetId);
        if (target == null) return;

        int damage = 40;
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
            Speed = 3.75f
        });
    }

    private void HandleAnnieQ(GameRoom room, GameObject caster, C_SkillCast skillPacket)
    {
        GameObject target = room.FindObject(skillPacket.TargetId);
        if (target == null) return;

        int damage = 65;
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
            Speed = 6.55f
        });
    }

    private void HandleAnnieW(GameRoom room, GameObject caster)
    {
        int tickCount = 3;
        int tickIntervalMs = 400;
        float range = 4.0f;
        int damage = 45;

        // ✅ 애니메이션은 처음에 한 번만 브로드캐스트
        room.Broadcast(new S_SkillResult
        {
            CasterId = caster.Info.ObjectId,
            SkillId = (int)SkillType.WSpell
        });

        for (int i = 0; i < tickCount; i++)
        {
            int delay = i * tickIntervalMs;

            room.PushAfter(delay, () =>
            {
                Vec3 center = new Vec3(caster.TileX, 0, caster.TileZ);
                var tiles = room._tilemap.GetTilesInRange(center, (int)range);

                foreach (Tile tile in tiles)
                {
                    foreach (GameObject obj in tile.GetPlayers())
                    {
                        if (obj == caster || obj.Info.TeamId == caster.Info.TeamId)
                            continue;

                        obj.Info.Hp = Math.Max(0, obj.Info.Hp - damage);

                        Console.WriteLine($"Damage 오는중?");

                        room.Broadcast(new S_Damage
                        {
                            TargetId = obj.Info.ObjectId,
                            Damage = damage,
                            RemainHp = obj.Info.Hp
                        });

                        if (obj.Info.Hp <= 0)
                        {
                            room.Broadcast(new S_Dead
                            {
                                TargetId = obj.Info.ObjectId
                            });
                        }
                    }
                }
            });
        }
    }

}
