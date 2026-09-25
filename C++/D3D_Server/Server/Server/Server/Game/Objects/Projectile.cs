using Google.Protobuf.Protocol;
using Server.Game.Objects;
using Server.Game.Room;
using System;
using Vec3 = System.Numerics.Vector3;

public class Projectile
{
    private readonly GameObject _caster;
    private readonly GameObject _target;
    private readonly float _speed;
    private readonly int _damage;
    private Vec3 _position;
    private bool _arrived;

    public ulong Id { get; private set; }
    public GameRoom Room { get; set; }
    public Vec3 Position => _position;
    public ulong CasterTeamId => (ulong)_caster.Info.TeamId;

    public Projectile(ulong id, GameObject caster, GameObject target, float speed, int damage)
    {
        Id = id;
        _caster = caster ?? throw new ArgumentNullException(nameof(caster));
        _target = target ?? throw new ArgumentNullException(nameof(target));
        _speed = speed;
        _damage = damage;
        _position = caster.Info.Position.ToNumericsVector3();
    }

    public void Update(float deltaTime)
    {
        if (_arrived || _target.Info == null || _caster.Info == null || Room == null)
            return;
        if (deltaTime <= 0.0f || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) || _speed <= 0.0f)
            return;

        Vec3 targetPosition = _target.Info.Position.ToNumericsVector3();
        Vec3 toTarget = targetPosition - _position;
        float distance = toTarget.Length();
        if (float.IsNaN(distance) || float.IsInfinity(distance))
            return;

        float moveDistance = _speed * deltaTime;
        if (distance > 0.35f && moveDistance < distance)
        {
            _position += Vec3.Normalize(toTarget) * moveDistance;
            return;
        }

        _position = targetPosition;
        _arrived = true;
        Room.Push(ResolveHit);
    }

    private void ResolveHit()
    {
        if (_target.Info == null || _caster.Info == null || _target.Info.TeamId == _caster.Info.TeamId)
        {
            Room.RemoveProjectile(this);
            return;
        }

        Room.Broadcast(new S_ProjectileHit
        {
            ProjectileId = Id,
            TargetId = _target.Info.ObjectId
        });

        _target.Info.Hp = Math.Max(0, _target.Info.Hp - _damage);
        Room.Broadcast(new S_Damage
        {
            TargetId = _target.Info.ObjectId,
            Damage = _damage,
            RemainHp = _target.Info.Hp
        });

        if (_target.Info.Hp <= 0)
        {
            if (_target is Player deadPlayer)
                Room.CancelPlayerMovement(deadPlayer);
            Room.Broadcast(new S_Dead { TargetId = _target.Info.ObjectId });
        }

        Room.RemoveProjectile(this);
    }
}