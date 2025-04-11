//using Google.Protobuf;
//using Google.Protobuf.Protocol;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Numerics;
//using Server.Game.Objects;
//using System.Threading;
//using ServerCore;

//using Vec3 = System.Numerics.Vector3;
//using System.Xml.Linq;
//using System.IO;
//using Server.Game.Room;
//public class Projectile
//{
//    public ulong ProjectileId { get; private set; }
//    public ulong CasterId { get; private set; }
//    public int CasterTeamId { get; private set; }
//    public ulong TargetId { get; private set; }
//    public Vec3 Position { get; private set; }
//    public Vec3 TargetPosition { get; private set; }
//    public float Speed { get; private set; }
//    public int Damage { get; private set; }

//    public GameRoom Room { get; set; } // ✅ GameRoom 참조 필요

//    private float _creationTime;


//    long _nextMoveTick = 0;

//    public Projectile(ulong projectileId, ulong casterId, ulong targetId,
//                      Vec3 startPos, Vec3 targetPos, float speed, int damage, int casterTeamId)
//    {
//        ProjectileId = projectileId;
//        CasterId = casterId;
//        TargetId = targetId;
//        Position = startPos;
//        TargetPosition = targetPos;
//        Speed = speed;
//        Damage = damage;
//        CasterTeamId = casterTeamId;
//        _creationTime = DateTime.UtcNow.Ticks / TimeSpan.TicksPerMillisecond;
//    }

//    public void Update()
//    {
//        long currentTime = Environment.TickCount64;

//        // ✅ 처음 Update() 호출 시, _nextMoveTick 초기화
//        if (_nextMoveTick == 0)
//        {
//            _nextMoveTick = currentTime;
//            return;
//        }

//        // ✅ 시간이 충분히 경과하지 않았다면 업데이트를 하지 않음
//        if (_nextMoveTick > currentTime)
//            return;

//        // ✅ 다음 이동 시간 계산 (DeltaTime 기반)
//        long tickInterval = (long)(1000 / Speed);
//        _nextMoveTick = currentTime + tickInterval;

//        // ✅ 이동 방향 계산
//        Vec3 direction = TargetPosition - Position;
//        if (direction.Length() < 0.01f)
//        {
//            Console.WriteLine($"[Projectile] {ProjectileId} has already reached the target.");
//            return;
//        }

//        // ✅ 이동 적용 (DeltaTime 기반)
//        Vec3 prevPos = Position;
//        Position += Vec3.Normalize(direction) * Speed * (tickInterval / 1000f);

//        // ✅ 같은 위치에서 계속 업데이트되는 경우 방지
//        if (prevPos == Position)
//        {
//            Console.WriteLine($"[Projectile] 🚫 No movement detected, skipping update.");
//            return;
//        }

//        Console.WriteLine($"[Projectile] {ProjectileId} Moved! Prev: {prevPos} → New: {Position} (Speed: {Speed}, Interval: {tickInterval}ms)");

//        // ✅ 타일맵 기반 충돌 체크
//        Vec3 tilePos = new Vec3((int)Position.X, 0, (int)Position.Z);
//        Console.WriteLine($"[Projectile] Checking collision at tilePos: {tilePos}");

//        GameObject target = Room.FindObject(TargetId);
//        if (target == null)
//        {
//            Console.WriteLine($"[Projectile] ❌ Target {TargetId} not found in Room.");
//            return;
//        }

//        Vec3 targetTilePos = new Vec3((int)target.Info.Position.X, 0, (int)target.Info.Position.Z);
//        Console.WriteLine($"[Projectile] Target {TargetId} found at tilePos: {targetTilePos}");

//        // ✅ 거리 기반 충돌 체크 (0.9 이내면 충돌로 간주)
//        float distance = (tilePos - targetTilePos).Length();
//        if (distance <= 0.9f)
//        {
//            Console.WriteLine($"[Projectile] 🎯 Collision detected! Projectile {ProjectileId} hit Target {TargetId}");

//            OnHit(target);
//            Room.RemoveProjectile(this);

//            // ✅ 클라이언트에 피격 정보 전달
//            S_ProjectileHit hitPacket = new S_ProjectileHit
//            {
//                ProjectileId = ProjectileId,
//                TargetId = target.Info.ObjectId
//            };
//            Room.Broadcast(hitPacket);
//        }
//        else
//        {
//            Console.WriteLine($"[Projectile] ❌ No collision. Distance: {distance}, Projectile at {tilePos}, Target at {targetTilePos}");
//        }
//    }



//    public void OnHit(GameObject target)
//    {
//        Console.WriteLine($"[Projectile] {ProjectileId} hit target {target?.Info.ObjectId}");

//        if (target != null)
//        {
//            target.Info.Hp -= Damage;
//            target.Info.Hp = Math.Max(0, target.Info.Hp);

//            // ✅ S_Damage 전송
//            S_Damage damagePacket = new S_Damage
//            {
//                TargetId = target.Info.ObjectId,
//                Damage = Damage,
//                RemainHp = target.Info.Hp
//            };
//            Room.Broadcast(damagePacket);

//            Console.WriteLine($"[Projectile] 📦 S_Damage Sent → Target: {target.Info.ObjectId}, RemainHp: {target.Info.Hp}");

//            // ✅ S_Dead 전송 (사망 체크)
//            if (target.Info.Hp <= 0)
//            {
//                S_Dead deadPacket = new S_Dead
//                {
//                    TargetId = target.Info.ObjectId
//                };
//                Room.Broadcast(deadPacket);
//                Console.WriteLine($"[Projectile] ☠️ Target {target.Info.ObjectId} is DEAD → S_Dead Sent");
//            }
//        }
//    }

//}



using Google.Protobuf;
using Google.Protobuf.Protocol;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Server.Game.Objects;
using System.Threading;
using ServerCore;

using Vec3 = System.Numerics.Vector3;
using System.Xml.Linq;
using System.IO;
using Server.Game.Room;

public class Projectile
{
    public ulong Id { get; private set; }
    public GameRoom Room { get; set; }

    private GameObject _caster;
    private GameObject _target;
    private float _speed;
    private int _damage;

    private Vec3 _position;
    private bool _arrived;

    public Projectile(ulong id, GameObject caster, GameObject target, float speed, int damage)
    {
        Id = id;
        _caster = caster;
        _target = target;
        _speed = speed;
        _damage = damage;
        _position = caster.Info.Position.ToNumericsVector3();
    }

    public void Update()
    {
        if (_arrived || _target == null || Room == null)
            return;

        // ✅ 방향 계산 및 이동
        Vec3 dir = Vec3.Normalize(_target.Info.Position.ToNumericsVector3() - _position);
        _position += dir * _speed * 0.113f; // 20fps 기준

        // ✅ 타일 좌표 기반 충돌 판정
        Vec3 projTile = new Vec3((int)_position.X, 0, (int)_position.Z);
        Vec3 targetTile = new Vec3((int)_target.Info.Position.X, 0, (int)_target.Info.Position.Z);

        if (projTile == targetTile)
        {
            _arrived = true;

            Room.Push(() =>
            {
                if (_target.Info.TeamId == _caster.Info.TeamId)
                    return;

                // 1. ✅ 피격 애니메이션/이펙트를 위한 Hit 패킷 전송
                Room.Broadcast(new S_ProjectileHit
                {
                    ProjectileId = this.Id,
                    TargetId = _target.Info.ObjectId
                });

                // 2. ✅ 데미지 적용 및 전송
                _target.Info.Hp = Math.Max(0, _target.Info.Hp - _damage);

                Room.Broadcast(new S_Damage
                {
                    TargetId = _target.Info.ObjectId,
                    Damage = _damage,
                    RemainHp = _target.Info.Hp
                });

                // 3. ✅ 사망 처리
                if (_target.Info.Hp <= 0)
                {
                    Room.Broadcast(new S_Dead
                    {
                        TargetId = _target.Info.ObjectId
                    });
                }

                // 4. ✅ 삭제
                Room.RemoveProjectile(this);
            });
        }
    }


    public Vec3 Position => _position;
    public ulong CasterTeamId => (ulong)_caster.Info.TeamId;
}

